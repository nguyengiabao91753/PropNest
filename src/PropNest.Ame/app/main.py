from fastapi import FastAPI
from fastapi.responses import RedirectResponse
from app.models.requests import ModerationRequest
from app.models.responses import ModerationResponse
from app.engine.rule_engine import RuleEngine

app = FastAPI(
    title="PropNest AME - AI Moderation Engine",
    version="1.0.0",
    docs_url="/docs"
)

engine = RuleEngine()


@app.get("/", include_in_schema=False)
async def root():
    return RedirectResponse(url="/docs")


@app.post("/api/v1/moderate", response_model=ModerationResponse)
async def moderate_listing(request: ModerationRequest) -> ModerationResponse:
    return engine.evaluate(request)


@app.get("/api/v1/health")
async def health():
    return {"status": "healthy", "service": "ame"}
