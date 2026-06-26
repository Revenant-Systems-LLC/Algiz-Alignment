import os
import time
from typing import Any

from fastapi import FastAPI, Request, HTTPException
from fastapi.responses import JSONResponse

from pipeline import GovernedEngine, create_default_provider
from sage_types import AgentConfig

app = FastAPI(title="Algiz OpenAI Proxy")

# Initialize the engine once
config = AgentConfig()
engine = GovernedEngine(create_default_provider(), config)
engine.start()

@app.post("/v1/chat/completions")
async def chat_completions(request: Request) -> JSONResponse:
    try:
        body = await request.json()
    except Exception:
        raise HTTPException(status_code=400, detail="Invalid JSON body")

    # Handle streaming
    if body.get("stream", False):
        # Explicitly document that streaming is not supported in the MVP due to post-flight checks.
        raise HTTPException(
            status_code=400, 
            detail="Streaming (stream=True) is not supported by the Algiz Proxy MVP due to post-flight guardrail requirements."
        )

    messages = body.get("messages", [])
    if not messages:
        raise HTTPException(status_code=400, detail="Messages array is required")

    # Extract the last user message
    last_user_message = ""
    for msg in reversed(messages):
        if msg.get("role") == "user":
            last_user_message = msg.get("content", "")
            break
    
    if not last_user_message:
        raise HTTPException(status_code=400, detail="No user message found in the messages array")

    # Process through the RAGE/SAGE pipeline
    try:
        response = engine.process(last_user_message)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

    # Format as an OpenAI compatible response
    created = int(time.time())
    model_used = body.get("model", "algiz-protected-model")

    openai_response = {
        "id": f"chatcmpl-algiz-{created}",
        "object": "chat.completion",
        "created": created,
        "model": model_used,
        "choices": [
            {
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": response.text
                },
                "logprobs": None,
                "finish_reason": "stop"
            }
        ],
        "usage": {
            "prompt_tokens": 0,
            "completion_tokens": 0,
            "total_tokens": 0
        },
        "system_fingerprint": "fp_algiz_governance"
    }

    return JSONResponse(content=openai_response)
