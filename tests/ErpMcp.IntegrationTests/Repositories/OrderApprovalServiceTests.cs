using ErpMcp.Application.Orders;
using ErpMcp.Domain.Common;
using ErpMcp.Domain.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace ErpMcp.IntegrationTests.Repositories;

public class OrderApprovalServiceTests(PostgresFixture db)
{
    private const string Approver = "ops.lead@example.com";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private T Get<T>() where T : notnull =>
        db.GetService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<T>();

    private async Task<string> NewDraftAsync()
    {
        var request = new DraftOrderRequest("CUST-0011", [new("DRY-0002", 5)]);
        return (await Get<DraftOrderService>().CreateAsync(request, "agent:tests", Ct)).Order.OrderNumber;
    }

    [Fact]
    public async Task Approve_records_who_and_when()
    {
        var number = await NewDraftAsync();

        var order = await Get<OrderApprovalService>().ApproveAsync(number, "Ops.Lead@Example.com", Ct);

        order.Status.ShouldBe(OrderStatus.Approved);
        order.DecidedBy.ShouldBe(Approver);
        order.DecidedAt.ShouldNotBeNull();
        order.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public async Task Reject_requires_and_stores_reason()
    {
        var number = await NewDraftAsync();
        var approvals = Get<OrderApprovalService>();

        await Should.ThrowAsync<ErpMcp.Application.Common.InputValidationException>(() => approvals.RejectAsync(number, Approver, " ", Ct));
        var order = await approvals.RejectAsync(number, Approver, "Customer asked to cancel", Ct);

        order.Status.ShouldBe(OrderStatus.Rejected);
        order.RejectionReason.ShouldBe("Customer asked to cancel");
    }

    [Fact]
    public async Task Decided_orders_cannot_be_decided_again()
    {
        var number = await NewDraftAsync();
        var approvals = Get<OrderApprovalService>();
        await approvals.ApproveAsync(number, Approver, Ct);

        var ex = await Should.ThrowAsync<DomainRuleViolationException>(() => approvals.RejectAsync(number, Approver, "Too late", Ct));
        ex.Message.ShouldContain("is approved");
    }

    [Fact]
    public async Task Agent_identities_cannot_approve()
    {
        var number = await NewDraftAsync();

        await Should.ThrowAsync<DomainRuleViolationException>(() => Get<OrderApprovalService>().ApproveAsync(number, "agent:helper", Ct));
    }

    [Fact]
    public async Task Concurrent_decisions_have_exactly_one_winner()
    {
        var number = await NewDraftAsync();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(async i =>
        {
            try
            {
                await Get<OrderApprovalService>().ApproveAsync(number, $"approver{i}@example.com", Ct);
                return true;
            }
            catch (DomainRuleViolationException)
            {
                return false;
            }
        }));

        attempts.Count(won => won).ShouldBe(1);
    }

    [Fact]
    public async Task Pending_queue_contains_new_drafts()
    {
        var number = await NewDraftAsync();

        var pending = await Get<OrderApprovalService>().ListPendingAsync(100, Ct);

        pending.Items.ShouldContain(o => o.OrderNumber == number);
        pending.Items.ShouldAllBe(o => o.Status == OrderStatus.PendingApproval);
    }
}
