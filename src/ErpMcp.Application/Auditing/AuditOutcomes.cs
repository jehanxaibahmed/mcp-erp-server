using ErpMcp.Application.Common;
using ErpMcp.Domain.Common;

namespace ErpMcp.Application.Auditing;

public static class AuditOutcomes
{
    /// <summary>Classifies an exception raised while handling an action.</summary>
    public static AuditOutcome Classify(Exception ex) => ex switch
    {
        PermissionDeniedException or AuditUnavailableException => AuditOutcome.Denied,
        InputValidationException => AuditOutcome.Invalid,
        NotFoundException => AuditOutcome.NotFound,
        DomainRuleViolationException => AuditOutcome.Rejected,
        _ => AuditOutcome.Failed,
    };
}
