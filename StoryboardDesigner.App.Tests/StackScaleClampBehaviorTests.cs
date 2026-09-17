using StoryboardDesigner.App.Models;
using Storyboard.Shared.Config;

namespace StoryboardDesigner.App.Tests;

public sealed class StackScaleClampBehaviorTests
{
    [Fact]
    public void ProjectModel_ClampsStackScaleDefaults_ToPolicyBounds()
    {
        var project = new ProjectModel
        {
            StackScaleStepDefault = 99,
            MinStackScaleDefault = 0.01
        };

        Assert.Equal(StackScalePolicy.MaxStackScaleStep, project.StackScaleStepDefault, precision: 6);
        Assert.Equal(StackScalePolicy.MinMinStackScale, project.MinStackScaleDefault, precision: 6);
    }

    [Fact]
    public void GameObject_ClampsStackScaleOverrides_ToPolicyBounds()
    {
        var obj = new GameObject
        {
            StackScaleStepOverride = 99,
            MinStackScaleOverride = 0.01
        };

        Assert.Equal(StackScalePolicy.MaxStackScaleStep, obj.StackScaleStepOverride);
        Assert.Equal(StackScalePolicy.MinMinStackScale, obj.MinStackScaleOverride);
    }
}
