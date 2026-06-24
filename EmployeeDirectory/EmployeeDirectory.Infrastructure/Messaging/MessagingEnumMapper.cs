using DomainAssignmentKind = EmployeeDirectory.Domain.Enums.OrgAssignmentKind;
using DomainNodeLevel = EmployeeDirectory.Domain.Enums.OrgNodeLevel;
using MsgAssignmentKind = Kyntus.Messaging.Contracts.OrgAssignmentKind;
using MsgNodeLevel = Kyntus.Messaging.Contracts.OrgNodeLevel;

namespace EmployeeDirectory.Infrastructure.Messaging;

internal static class MessagingEnumMapper
{
    public static MsgAssignmentKind ToMessage(DomainAssignmentKind kind) => (MsgAssignmentKind)(int)kind;
    public static MsgNodeLevel ToMessage(DomainNodeLevel level) => (MsgNodeLevel)(int)level;
    public static DomainAssignmentKind FromMessage(MsgAssignmentKind kind) => (DomainAssignmentKind)(int)kind;
    public static DomainNodeLevel FromMessage(MsgNodeLevel level) => (DomainNodeLevel)(int)level;
}
