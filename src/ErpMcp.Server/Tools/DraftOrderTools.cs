using System.ComponentModel;
using ErpMcp.Application.Common;
using ErpMcp.Application.Orders;
using ErpMcp.Application.Security;
using ErpMcp.Domain.Orders;
using ErpMcp.Server.Auditing;
using ErpMcp.Server.Security;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ErpMcp.Server.Tools;

[McpServerToolType]
public sealed class DraftOrderTools(DraftOrderService drafts, ToolCallAnnotations audit)
{
    [RequiresScope(Scopes.OrdersDraft)]
    [McpServerTool(Name = "create_draft_order", Title = "Create draft order",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Draft a sales order for a customer. The order is NOT placed: it goes into a queue for a person
        to approve or reject, and nothing ships until they do. Prices always come from the catalogue.
        The response lists any review flags (e.g. credit limit or stock shortfall) the approver will see.
        Pass an idempotencyKey so retrying after a timeout returns the same draft instead of a duplicate.
        """)]
    public async Task<DraftOrderResponse> CreateDraftOrder(
        RequestContext<CallToolRequestParams> context,
        [Description("Customer code, e.g. CUST-0001. The account must be active.")] string customerCode,
        [Description("Order lines (1-50). Each SKU may appear once.")] OrderLineInput[] lines,
        [Description("Optional delivery or handling notes for the warehouse, max 500 characters.")] string? notes = null,
        [Description("Optional unique key (8-100 chars) to make retries safe, e.g. a UUID.")] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        var request = new DraftOrderRequest(
            customerCode,
            lines?.Select(l => new DraftOrderLineRequest(l.Sku, l.Quantity)).ToList(),
            notes,
            idempotencyKey);

        var requester = new Requester(
            AgentActor.FromClientName(context.Server.ClientInfo?.Name),
            CallerIdentity.PersonFrom(context.User));
        var result = await drafts.CreateAsync(request, requester, ct);
        audit.EntityRef = result.Order.OrderNumber;

        var message = result.AlreadyExisted
            ? $"Draft {result.Order.OrderNumber} already existed for this idempotency key. No new order was created."
            : $"Draft {result.Order.OrderNumber} created and is pending human approval. It will not be fulfilled until approved.";

        return new DraftOrderResponse(message, result.AlreadyExisted, result.Order);
    }

    public sealed record OrderLineInput(
        [property: Description("Product SKU, e.g. BEV-0001.")] string Sku,
        [property: Description("Quantity in the product's unit of measure, 1-10000.")] int Quantity);

    public sealed record DraftOrderResponse(string Message, bool AlreadyExisted, Order Order);
}
