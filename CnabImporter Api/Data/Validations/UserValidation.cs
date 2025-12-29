namespace Api.Data.Validations;

public sealed class UserCreateValidator : Validation<User>
{
    public UserCreateValidator()
	{
		RuleFor(p => p.FirstName).NotEmpty().NotNull();
		RuleFor(p => p.LastName).NotEmpty().NotNull();
		RuleFor(p => p.Email).NotEmpty().NotNull().EmailAddress();
        RuleFor(p => p.SignInWith).NotEmpty().NotNull();
        RuleFor(p => p.Type).NotNull();
        When(p => !string.IsNullOrEmpty(p.SignInWith) && p.SignInWith.Equals(SignIn.Default.ToString(), StringComparison.CurrentCultureIgnoreCase),
            () => RuleFor(p => p.Password).NotEmpty().NotNull());
        When(p => !string.IsNullOrEmpty(p.SignInWith) && p.SignInWith.Equals(SignIn.Default.ToString(), StringComparison.CurrentCultureIgnoreCase),
            () => RuleFor(p => p.Cpf).NotEmpty().NotNull());
    }
}

public sealed class UserUpdateValidator : Validation<User>
{
    public UserUpdateValidator()
    {
        RuleFor(r => r.Id).NotNull().NotEmpty();
        RuleFor(p => p.FirstName).NotEmpty().NotNull();
        RuleFor(p => p.LastName).NotEmpty().NotNull();
        RuleFor(p => p.BirthDate).NotEmpty().NotNull();
        RuleFor(p => p.Cpf).NotEmpty().NotNull();
    }
}

public sealed class UserPatchValidator : Validation<User>
{
    public UserPatchValidator()
    {
        RuleFor(r => r.Id).NotNull().NotEmpty();
    }
}

public sealed class UserLogCreateValidator : Validation<UserLog>
{
    public UserLogCreateValidator()
    {
        RuleFor(p => p.UserId).NotEmpty().NotNull();
        RuleFor(p => p.Log).NotEmpty().NotNull();
    }
}

public static class UserValidationExtension
{
    public static Task<ValidationResult> ValidateCreateAsync(this User u) => new UserCreateValidator().ValidateCustomAsync(u);
    public static Task<ValidationResult> ValidateUpdateAsync(this User u) => new UserUpdateValidator().ValidateCustomAsync(u);
    public static Task<ValidationResult> ValidatePatchAsync(this User u) => new UserPatchValidator().ValidateCustomAsync(u);
    public static Task<ValidationResult> ValidateCreateUserLogAsync(this UserLog u) => new UserLogCreateValidator().ValidateCustomAsync(u);
}