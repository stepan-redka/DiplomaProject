using Diploma.Application.Interfaces.AI;

namespace Diploma.Infrastructure.Services.AI;

public class PromptRegistry : IPromptRegistry
{
    public string GetGeneralPrompt(string question)
    {
        return $"""
            The user asked: '{question}'. 
            You are a professional AI research assistant. Respond naturally and concisely. 
            If it's a greeting, acknowledge it politely and ask how you can assist with their research repository. 
            Do not mention searching a database.
            """;
    }

    public string GetRagPrompt(string question, string context)
    {
        return $"""
            You are a professional research assistant. Use the following pieces of retrieved context to answer the user's question.
            If you don't know the answer based on the context, state that the repository doesn't have specific data, then provide a general answer if possible but CLEARLY distinguish it.
            Always prefer information from the context.
            
            CONTEXT:
            ---------------------
            {context}
            ---------------------
            
            QUERY: {question}
            
            INSTRUCTIONS:
            1. Answer accurately based on the context.
            2. Use a professional, academic tone.
            3. If multiple sources are provided, synthesize them.
            
            ANSWER:
            """;
    }

    public string GetEmptyRepoPrompt(string question)
    {
        return $"""
            The user asked: '{question}'. 
            Note: There are currently NO documents in their research repository. 
            Answer their question to the best of your ability as a helpful research assistant, but keep the response concise.
            """;
    }
}
