curl -s -X POST http://localhost:5000/api/Chat/Ask \
     -H "Content-Type: application/json" \
     -d '{"question":"What is RAG?","topK":3,"isHighFidelity":false}' > /home/stepan/Documents/Uni/4course/Diploma/RagSystem/api_out.json
cat /home/stepan/Documents/Uni/4course/Diploma/RagSystem/api_out.json
