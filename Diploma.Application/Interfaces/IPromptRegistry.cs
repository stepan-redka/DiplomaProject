namespace Diploma.Application.Interfaces;

public interface IPromptRegistry
{
    string GetGeneralPrompt(string question);
    string GetRagPrompt(string question, string context);
    string GetEmptyRepoPrompt(string question);
    string GetIntentResolutionPrompt(string question);
}
