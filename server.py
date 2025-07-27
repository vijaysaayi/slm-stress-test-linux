import os
import time
import uuid
import logging
from typing import Any, Dict, List, Optional

from fastapi import FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from transformers import (
    AutoModelForCausalLM,
    AutoTokenizer,
    TextGenerationPipeline,
)
import torch

# Configure structured logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s %(levelname)s %(message)s')
LOG = logging.getLogger("inference_api")

MODEL_DIR = os.getenv("MODEL_DIR", "model")


# ----- Pydantic models for OpenAI compatibility -----

class CompletionRequest(BaseModel):
    model: Optional[str] = None
    prompt: Optional[List[str]] = None
    max_tokens: Optional[int] = 16
    max_new_tokens: Optional[int] = None  # Add support for max_new_tokens
    temperature: Optional[float] = 1.0
    top_p: Optional[float] = 1.0
    n: Optional[int] = 1
    stream: Optional[bool] = False
    stop: Optional[List[str]] = None

class TextChoice(BaseModel):
    text: str
    index: int
    logprobs: Optional[Dict[str, Any]] = None
    finish_reason: Optional[str] = None

class CompletionResponse(BaseModel):
    id: str
    object: str = "text_completion"
    created: int
    model: str
    choices: List[TextChoice]
    usage: Dict[str, int]

class ChatMessage(BaseModel):
    role: str
    content: Optional[str] = None  # Allow None for function calls

class FunctionSpec(BaseModel):
    name: str
    description: Optional[str]
    parameters: Dict[str, Any]

class ChatCompletionRequest(BaseModel):
    model: Optional[str] = None
    messages: List[ChatMessage]
    functions: Optional[List[FunctionSpec]] = None
    function_call: Optional[Any] = None
    max_tokens: Optional[int] = 16
    max_new_tokens: Optional[int] = None  # Add support for max_new_tokens
    temperature: Optional[float] = 1.0
    top_p: Optional[float] = 1.0
    n: Optional[int] = 1
    stream: Optional[bool] = False
    stop: Optional[List[str]] = None

class ChatChoice(BaseModel):
    index: int
    message: ChatMessage
    finish_reason: Optional[str] = None
    function_call: Optional[Dict[str, Any]] = None

class ChatCompletionResponse(BaseModel):
    id: str
    object: str = "chat.completion"
    created: int
    model: str
    choices: List[ChatChoice]
    usage: Dict[str, int]

# ----- Initialize FastAPI app -----

app = FastAPI(title="Production-Grade OpenAI-Compatible Inference API")


# ----- Custom exception handlers -----

@app.exception_handler(HTTPException)
async def http_exception_handler(request: Request, exc: HTTPException):
    LOG.error(f"HTTP error: {exc.detail}")
    return JSONResponse(
        status_code=exc.status_code,
        content={
            "error": {
                "message": exc.detail,
                "type": "invalid_request_error",
                "param": None,
                "code": None
            }
        },
    )

@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError):
    LOG.error(f"Validation error: {exc}")
    return JSONResponse(
        status_code=422,
        content={
            "error": {
                "message": "Validation failed",
                "type": "validation_error",
                "param": None,
                "code": None,
                "details": exc.errors()
            }
        },
    )

@app.exception_handler(Exception)
async def generic_exception_handler(request: Request, exc: Exception):
    LOG.exception("Unexpected error")
    return JSONResponse(
        status_code=500,
        content={
            "error": {
                "message": "Internal server error",
                "type": "server_error",
                "param": None,
                "code": None
            }
        },
    )


# ----- Load model once at startup -----
try:
    tokenizer = AutoTokenizer.from_pretrained(MODEL_DIR, trust_remote_code=True)
    model = AutoModelForCausalLM.from_pretrained(
        MODEL_DIR,
        torch_dtype=torch.float32,
        low_cpu_mem_usage=True,
    )
    pipeline = TextGenerationPipeline(
        model=model,
        tokenizer=tokenizer,
        device=-1,  # CPU
    )
    LOG.info("Model loaded successfully")
except Exception as e:
    LOG.exception("Failed to load model at startup")
    raise


@app.get("/health")
async def health():
    return {"status": "ok"}


@app.post("/v1/completions", response_model=CompletionResponse)
async def completions(req: CompletionRequest):
    if not req.prompt:
        raise HTTPException(status_code=400, detail="`prompt` is required")
    prompt = req.prompt[0]
    
    # Use max_new_tokens if provided, otherwise fall back to max_tokens
    max_tokens_to_use = req.max_new_tokens if req.max_new_tokens is not None else req.max_tokens
    
    try:
        outputs = pipeline(
            prompt,
            max_new_tokens=max_tokens_to_use,
            do_sample=req.temperature > 0,
            temperature=req.temperature,
            top_p=req.top_p,
            num_return_sequences=req.n,
            eos_token_id=tokenizer.eos_token_id,
            pad_token_id=tokenizer.eos_token_id,
        )
    except Exception as e:
        LOG.exception("Generation failed")
        raise HTTPException(status_code=500, detail="Text generation failed")

    choices = []
    usage = {"prompt_tokens": len(tokenizer(prompt, truncation=True, max_length=512).input_ids), "completion_tokens": 0, "total_tokens": 0}

    for i, out in enumerate(outputs):
        text = out["generated_text"][len(prompt):]
        tok_out = tokenizer(text, truncation=True, max_length=512).input_ids
        usage["completion_tokens"] += len(tok_out)
        choices.append(TextChoice(text=text, index=i, finish_reason="stop"))

    usage["total_tokens"] = usage["prompt_tokens"] + usage["completion_tokens"]
    return CompletionResponse(
        id=str(uuid.uuid4()),
        created=int(time.time()),
        model=req.model or MODEL_DIR,
        choices=choices,
        usage=usage
    )


@app.post("/v1/chat/completions", response_model=ChatCompletionResponse)
async def chat_completions(req: ChatCompletionRequest):
    user_msgs = [m.content for m in req.messages if m.role == "user"]
    if not user_msgs:
        raise HTTPException(status_code=400, detail="No user messages provided")
    prompt = user_msgs[-1]

    # Use max_new_tokens if provided, otherwise fall back to max_tokens
    max_tokens_to_use = req.max_new_tokens if req.max_new_tokens is not None else req.max_tokens

    # If functions specified, return a function_call stub
    if req.functions:
        func = req.functions[0]
        return ChatCompletionResponse(
            id=str(uuid.uuid4()),
            created=int(time.time()),
            model=req.model or MODEL_DIR,
            choices=[ChatChoice(
                index=0,
                message=ChatMessage(role="assistant", content=""),
                finish_reason="function_call",
                function_call={"name": func.name, "arguments": "{}"}
            )],
            usage={"prompt_tokens": len(tokenizer(prompt, truncation=True, max_length=512).input_ids), "completion_tokens": 0, "total_tokens": len(tokenizer(prompt, truncation=True, max_length=512).input_ids)}
        )

    try:
        outputs = pipeline(
            prompt,
            max_new_tokens=max_tokens_to_use,
            do_sample=req.temperature > 0,
            temperature=req.temperature,
            top_p=req.top_p,
            num_return_sequences=req.n,
            eos_token_id=tokenizer.eos_token_id,
            pad_token_id=tokenizer.eos_token_id,
        )
    except Exception:
        LOG.exception("Chat generation failed")
        raise HTTPException(status_code=500, detail="Chat generation failed")

    choices = []
    usage = {"prompt_tokens": len(tokenizer(prompt, truncation=True, max_length=512).input_ids), "completion_tokens": 0, "total_tokens": 0}

    for i, out in enumerate(outputs):
        text = out["generated_text"][len(prompt):]
        tok_out = tokenizer(text, truncation=True, max_length=512).input_ids
        usage["completion_tokens"] += len(tok_out)
        choices.append(ChatChoice(
            index=i,
            message=ChatMessage(role="assistant", content=text),
            finish_reason="stop"
        ))

    usage["total_tokens"] = usage["prompt_tokens"] + usage["completion_tokens"]
    return ChatCompletionResponse(
        id=str(uuid.uuid4()),
        created=int(time.time()),
        model=req.model or MODEL_DIR,
        choices=choices,
        usage=usage
    )


@app.post("/v1/tools/{tool_name}")
async def call_tool(tool_name: str, args: Dict[str, Any]):
    """
    Execute an external tool by name with provided arguments.
    """
    try:
        # TODO: Replace with real tool execution logic
        result = {"tool_name": tool_name, "output": f"Executed {tool_name} with args {args}"}
        LOG.info(f"Tool {tool_name} executed")
        return result
    except Exception:
        LOG.exception(f"Tool execution failed: {tool_name}")
        raise HTTPException(status_code=500, detail=f"Tool execution failed for {tool_name}")
