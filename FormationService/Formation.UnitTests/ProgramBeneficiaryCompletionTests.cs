using Formation.Domain;
using Xunit;

namespace Formation.UnitTests;

public class ProgramBeneficiaryCompletionTests
{
    [Fact]
    public void IsComplete_RequiresAttendance()
    {
        Assert.False(ProgramBeneficiaryCompletion.IsComplete(
            attendedInPerson: false,
            hasContentTrack: false,
            contentCompleted: false,
            hasQuizTrack: false,
            quizPassed: false));
    }

    [Fact]
    public void IsComplete_IgnoresMissingTracks()
    {
        Assert.True(ProgramBeneficiaryCompletion.IsComplete(
            attendedInPerson: true,
            hasContentTrack: false,
            contentCompleted: false,
            hasQuizTrack: false,
            quizPassed: false));
    }

    [Fact]
    public void IsComplete_RequiresContentAndQuizWhenPresent()
    {
        Assert.False(ProgramBeneficiaryCompletion.IsComplete(true, true, false, true, true));
        Assert.False(ProgramBeneficiaryCompletion.IsComplete(true, true, true, true, false));
        Assert.True(ProgramBeneficiaryCompletion.IsComplete(true, true, true, true, true));
    }

    [Fact]
    public void ShouldAssignToSession_SkipsCompletedAndAlreadyAssigned()
    {
        var emp = Guid.NewGuid();
        var other = Guid.NewGuid();
        var already = new HashSet<Guid> { emp };
        var completed = new HashSet<Guid> { other };

        Assert.False(ProgramBeneficiaryCompletion.ShouldAssignToSession(emp, already, completed));
        Assert.False(ProgramBeneficiaryCompletion.ShouldAssignToSession(other, already, completed));
        Assert.True(ProgramBeneficiaryCompletion.ShouldAssignToSession(Guid.NewGuid(), already, completed));
    }
}
