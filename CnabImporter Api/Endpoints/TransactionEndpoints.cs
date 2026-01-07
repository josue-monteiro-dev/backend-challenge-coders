using Polly;

namespace Api.Endpoints;

public static class TransactionEndpoints
{
    public static void MapTransactionEndpoints(this WebApplication app)
    {
        const string ModelName = nameof(Transaction);
        var tag = new List<OpenApiTag> { new() { Name = ModelName } };

        app.MapGet("/api/transactions/{id:long}",
        async (long id, [FromServices] ITransactionService service) =>
        {
            var entitie = await service.GetByIdAsync(id);
            if (entitie is null) return Results.NoContent();
            return Results.Ok(entitie);
        })
        .Produces((int)HttpStatusCode.OK, typeof(Transaction))
        .WithName($"{ModelName}ById")
        .WithOpenApi(x => new OpenApiOperation(x)
        {
            Summary = $"Returns one {ModelName}",
            Description = $"This endpoint receives an Id from the header and searches for it in the {ModelName}s table. It produces a 200 status code.",
            Tags = tag
        })
        .RequireAuthorization("Get");

        app.MapGet("/api/transactions",
        async ([AsParameters] TransactionFilters filters, [FromServices] ITransactionService service) =>
        {
            var entities = await service.GetAllAsync(filters);
            if (entities.Count == 0) return Results.NoContent();
            return Results.Ok(entities);
        })
        .Produces((int)HttpStatusCode.OK, typeof(List<Transaction>))
        .WithName($"All{ModelName}")
        .WithOpenApi(x => new OpenApiOperation(x)
        {
            Summary = $"Get all {ModelName}",
            Description = $"This endpoint searches for all records in the {ModelName}s table. It produces a 200 status code.",
            Tags = tag
        })
        .RequireAuthorization("Admin");

        app.MapPost("/api/transactions",
        async (
            Transaction model,
            [FromServices] ITransactionService service,
            [FromServices] INotificationService notification,
			HttpContext context) =>
		{
			var loggedUserId = TokenExtension.GetUserIdFromToken(context);
			var entitie = await service.CreateAsync(model, loggedUserId);
            if (notification.HasNotifications) return Results.BadRequest(notification.Notifications);
            return Results.Created($"/transactions/{entitie!.Id}", entitie);
        })
        .Produces((int)HttpStatusCode.Created)
        .WithName($"Create{ModelName}")
        .WithOpenApi(x => new OpenApiOperation(x)
        {
            Summary = $"Create a new {ModelName}",
            Description = $"This endpoint receives a {ModelName} object as the request body and add it in the {ModelName}s table. It produces a 201 status code.",
            Tags = tag
        })
        .RequireAuthorization("Admin");

        app.MapPut("/api/transactions",
        async (
            Transaction model,
            [FromServices] ITransactionService service,
            [FromServices] INotificationService notification,
            HttpContext context) =>
        {
            var loggedUserName = TokenExtension.GetUserNameFromToken(context);
            var entitie = await service.UpdateAsync(model, loggedUserName);
            if (notification.HasNotifications) return Results.BadRequest(notification.Notifications);
            if (entitie is null) return Results.NoContent();
            return Results.Ok(entitie);
        })
        .Produces((int)HttpStatusCode.OK)
        .WithName($"Update{ModelName}")
        .WithOpenApi(x => new OpenApiOperation(x)
        {
            Summary = $"Updates one {ModelName}",
            Description = $"This endpoint receives an Id through the header and a {ModelName} object as the request body and updates it in the {ModelName}s table. It produces a 201 status code.",
            Tags = tag
        })
        .RequireAuthorization("Update");

        app.MapPatch("/api/transactions",
        async (
            Transaction model,
            [FromServices] ITransactionService service,
            [FromServices] INotificationService notification,
            HttpContext context) =>
		{
			var loggedUserName = TokenExtension.GetUserNameFromToken(context);
			var entitie = await service.PatchAsync(model, loggedUserName);
			if (notification.HasNotifications) return Results.BadRequest(notification.Notifications);
            if (entitie is null) return Results.NoContent();
            return Results.Ok(entitie);
        })
        .Produces((int)HttpStatusCode.OK)
        .WithName($"Patch{ModelName}")
        .WithOpenApi(x => new OpenApiOperation(x)
        {
            Summary = $"Activates/Deactivates an {ModelName}.",
            Description = $"This endpoint receives an Id through the header and a {ModelName} object as the request body and updates only changed properties it in the {ModelName}s table. It produces a 201 status code.",
            Tags = tag
        })
        .RequireAuthorization("Update");

        app.MapDelete("/api/transactions/{id:long}",
        async (
            long id,
            [FromServices] ITransactionService service,
            [FromServices] INotificationService notification,
            HttpContext context) =>
		{
			var loggedUserName = TokenExtension.GetUserNameFromToken(context);
			var result = await service.DeleteAsync(id, loggedUserName);
            if (notification.HasNotifications) return Results.BadRequest(notification.Notifications);
            if (result == null) return Results.NoContent();
            return Results.Ok(result);
        })
        .Produces((int)HttpStatusCode.OK)
        .WithName($"Delete{ModelName}")
        .WithOpenApi(x => new OpenApiOperation(x)
        {
            Summary = $"Delete one {ModelName}",
            Description = $"This endpoint receives an Id from the header and deletes it from the {ModelName}s table. It produces a 200 status code.",
            Tags = tag
        })
        .RequireAuthorization("Admin");

		//UPLOAD FILE
		app.MapPost("/api/transactions/upload-from-file",
		async (
			IFormFile file,
			[FromServices] ITransactionService service,
			[FromServices] INotificationService notification,
			HttpContext context) =>
		{
			var loggedUserId = TokenExtension.GetUserIdFromToken(context);
			var loggedUserName = TokenExtension.GetUserNameFromToken(context);
			var result = await service.UploadFileWithTransactions(file, loggedUserId, loggedUserName);
			if (notification.HasNotifications) return Results.BadRequest(notification.Notifications);
			return Results.Ok(result);
		})
		.Produces((int)HttpStatusCode.OK)
		.WithName("uploadPhoto")
		.WithOpenApi(x => new OpenApiOperation(x)
		{
			Summary = "Upload file with Transactions Data",
			Description = "This endpoint receives a File as the request body to process. It produces a 200 status code.",
			Tags = tag
		}).DisableAntiforgery()
		.RequireAuthorization("Admin");
	}
}