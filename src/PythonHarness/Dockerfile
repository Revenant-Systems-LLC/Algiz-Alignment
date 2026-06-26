FROM python:3.12-slim

WORKDIR /app

# Copy requirements
COPY requirements.txt .

# Install dependencies
RUN pip install --no-cache-dir -r requirements.txt
RUN pip install --no-cache-dir fastapi uvicorn

# Copy the rest of the application
COPY . .

# Expose the proxy port
EXPOSE 8000

# Start the proxy server
CMD ["uvicorn", "proxy:app", "--host", "0.0.0.0", "--port", "8000"]
