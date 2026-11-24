from pathlib import Path

from langchain_chroma import Chroma 
from langchain_ollama import OllamaEmbeddings
from langchain_text_splitters import RecursiveCharacterTextSplitter

#embedding model
embeddings = OllamaEmbeddings(model="nomic-embed-text")

#split
spliter = RecursiveCharacterTextSplitter(
    chunk_size=500,
    chunk_overlap=75)

#save in chroma 
database_path = Path(__file__).parent / "chroma_data"

store = Chroma(
    collection_name="paperflow_documents",
    embedding_function=embeddings,
    persist_directory=str(database_path)
)

def add_document(document_id, filename, text):
    chunks = spliter.split_text(text)

    metadata = []
    chunk_ids = []

    for index in range(len(chunks)):
        metadata.append(
            {
                "document_id": document_id,
                "filename": filename,
                "chunk_number": index + 1
            }
        )

        chunk_ids.append(f"{document_id}-{index}")

    store.add_texts(
        texts=chunks,
        metadatas=metadata,
        ids=chunk_ids
    )

    return len(chunks)

def find_passages(document_id, question):
    passages = store.similarity_search(
        question,
        k=3,
        filter = {"document_id": document_id}
    )

    return "\n\n".join(passage.page_content for passage in passages)

def list_documents():
    result = store.get(include=["metadatas"])

    documents = {}

    for metadata in result["metadatas"]:
        document_id = metadata["document_id"]

        if document_id not in documents:
            documents[document_id] = {
                "document_id": document_id,
                "filename": metadata["filename"],
                "chunk_count": 0
            }

        documents[document_id]["chunk_count"] += 1

    return list(documents.values())

def find_passages_from_documents(document_ids, question):
    passages = store.similarity_search(
        question,
        k=6,
        filter={"document_id": {"$in": document_ids}},
    )

    text_parts = []

    for passage in passages:
        filename = passage.metadata["filename"]

        text_parts.append(
            f"Document: {filename}\n{passage.page_content}"
        )

    return "\n\n".join(text_parts)

