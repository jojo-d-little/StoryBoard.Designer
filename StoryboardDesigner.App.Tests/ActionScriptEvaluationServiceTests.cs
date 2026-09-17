namespace StoryboardDesigner.App.Tests;

public sealed class ActionScriptEvaluationServiceTests
{
    [Fact]
    public void Evaluate_IfNotCondition_DoesNotEmitOutput_WhenHiddenIsTrue()
    {
        var sut = new ActionScriptEvaluationService();

        var result = sut.Evaluate(new ActionScriptEvaluationRequest
        {
            ScriptText = "IF NOT{self.isHidden}\nThere is a {self.name}\nENDIF",
            ReferenceTokens = new[] { "self.isHidden", "self.name" },
            ReferenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["self.isHidden"] = "true",
                ["self.name"] = "SmokeBall"
            }
        });

        Assert.True(result.IsSyntaxValid);
        Assert.Equal(string.Empty, result.OutputText);
    }

    [Fact]
    public void Evaluate_IfNotCondition_EmitsOutput_WhenHiddenIsFalse()
    {
        var sut = new ActionScriptEvaluationService();

        var result = sut.Evaluate(new ActionScriptEvaluationRequest
        {
            ScriptText = "IF NOT{self.isHidden}\nThere is a {self.name}\nENDIF",
            ReferenceTokens = new[] { "self.isHidden", "self.name" },
            ReferenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["self.isHidden"] = "false",
                ["self.name"] = "SmokeBall"
            }
        });

        Assert.True(result.IsSyntaxValid);
        Assert.Equal("There is a SmokeBall", result.OutputText);
    }
}
