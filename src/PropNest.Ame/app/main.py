from fastapi import FastAPI
from fastapi.responses import RedirectResponse
from app.models.requests import ModerationRequest
from app.models.responses import ModerationResponse, Decision
from app.engine.rule_engine import RuleEngine
from app.engine.ollama_analyzer import OllamaAnalyzer
from app.engine.decision_aggregator import DecisionAggregator

app = FastAPI(
    title="PropNest AME - AI Moderation Engine",
    version="2.0.0",
    docs_url="/docs"
)

rule_engine = RuleEngine()
ollama_analyzer = OllamaAnalyzer()
decision_aggregator = DecisionAggregator()


@app.get("/", include_in_schema=False)
async def root():
    return RedirectResponse(url="/docs")


@app.post("/api/v1/moderate", response_model=ModerationResponse)
async def moderate_listing(request: ModerationRequest) -> ModerationResponse:
    # Bước 1: Rule Engine (đồng bộ, cực nhanh)
    rule_result = rule_engine.evaluate(request)

    # Short-circuit: Vi phạm luật cứng → trả ngay, không gọi LLM
    if rule_result.decision == Decision.REJECTED:
        return rule_result

    # Bước 2: Ollama LLM (bất đồng bộ)
    llm_result = await ollama_analyzer.analyze(request)

    # Bước 3: Tổng hợp quyết định
    return decision_aggregator.aggregate(
        listing_id=request.listing_id,
        rule_response=rule_result,
        llm_result=llm_result
    )


@app.get("/api/v1/health")
async def health():
    return {"status": "healthy", "service": "ame", "version": "2.0.0"}
