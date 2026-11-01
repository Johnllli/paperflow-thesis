from fastapi import FastAPI
from pydantic import BaseModel
#server name app
app = FastAPI()
#define request name and type
class TextRequest(BaseModel):
    text: str
#when starting the server, it will return this message
@app.get("/")
def home():
    return {"status": "Python FastAPI is running"}

#return way and where is it
@app.post("/transform")
def transform_text(request: TextRequest):
    modified = f"Python processed your text: {request.text.upper()}"
    return {"result": modified}
