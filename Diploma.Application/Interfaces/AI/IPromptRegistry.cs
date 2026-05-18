namespace Diploma.Application.Interfaces.AI;

public interface IPromptRegistry
{
    string GetGeneralPrompt(string question);
    string GetRagPrompt(string question, string context);
    string GetEmptyRepoPrompt(string question);
}
