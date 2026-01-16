using Api.Endpoints;
using Api.Helpers.Middlewares;

namespace Api.Helpers.Extensions;

public static class AppExtensions
{
    public static void UseArchitectures(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        //app.UseAuthentication();
        //app.UseAuthorization();
        app.UseExceptionHandleMiddleware();

        app.MapLoginEndpoints();
        app.MapUserEndpoints();
		app.MapTransactionEndpoints();
		app.MapTransactionTypeEndpoints();
	}

	public static IApplicationBuilder UseExceptionHandleMiddleware(this IApplicationBuilder builder)
        => builder.UseMiddleware<ExceptionHandleMiddleware>();
}