using HomeDaemon.Application.Scheduling;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;

namespace HomeDaemon.UnitTests.Scheduling;

public sealed class MemberAssignmentPolicyResolverTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly HouseholdMember[] Members =
    [
        new() { Id = Alice, IsAssignable = true },
        new() { Id = Bob, IsAssignable = true },
        new() { Id = Guid.NewGuid(), IsAssignable = false },
    ];

    [Fact]
    public void Both_AllowsEveryAssignableMember()
    {
        var chore = new ChoreDefinition { AssignmentPolicy = AssignmentPolicy.Both };

        var result = MemberAssignmentPolicyResolver.Resolve(chore, Members);

        result.Should().BeEquivalentTo([Alice, Bob]);
    }

    [Fact]
    public void SpecificMember_AllowsOnlyTheFixedAssignee()
    {
        var chore = new ChoreDefinition { AssignmentPolicy = AssignmentPolicy.SpecificMember, FixedAssigneeMemberId = Bob };

        var result = MemberAssignmentPolicyResolver.Resolve(chore, Members);

        result.Should().Equal(Bob);
    }

    [Fact]
    public void SpecificMember_ReturnsEmptyWhenTheFixedAssigneeIsNotAssignable()
    {
        var chore = new ChoreDefinition { AssignmentPolicy = AssignmentPolicy.SpecificMember, FixedAssigneeMemberId = Guid.NewGuid() };

        var result = MemberAssignmentPolicyResolver.Resolve(chore, Members);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Alternating_ExcludesTheLastCompleter()
    {
        var chore = new ChoreDefinition { AssignmentPolicy = AssignmentPolicy.Alternating, LastCompletedByMemberId = Alice };

        var result = MemberAssignmentPolicyResolver.Resolve(chore, Members);

        result.Should().Equal(Bob);
    }

    [Fact]
    public void Alternating_FallsBackToAllMembersWhenAlternationIsImpossible()
    {
        var chore = new ChoreDefinition { AssignmentPolicy = AssignmentPolicy.Alternating, LastCompletedByMemberId = Alice };
        HouseholdMember[] singleMember = [new() { Id = Alice, IsAssignable = true }];

        var result = MemberAssignmentPolicyResolver.Resolve(chore, singleMember);

        result.Should().Equal(Alice);
    }
}
