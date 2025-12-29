using Api.Data.Context;
using Api.Data.Dtos;
using Api.Data.Entities;
using Api.Data.Enums;
using Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CnabImporter.Tests.Services;

public class UserServiceTests
{
	protected readonly CnabDbContext _db;
	protected readonly Mock<INotificationService> _notificationMock;
	protected readonly UserService _service;

	public UserServiceTests()
	{
		var options = new DbContextOptionsBuilder<CnabDbContext>()
			.UseSqlite("Data Source=CnabImporterTests.db;Cache=Shared")
			.EnableSensitiveDataLogging()
			.Options;

		_db = new CnabDbContext(options);
		_db.Database.EnsureDeleted();
		_db.Database.EnsureCreated();

		_notificationMock = new Mock<INotificationService>();

		_service = new UserService(
			_db,
			_notificationMock.Object
		);
	}

	private static User NewValidUser(long id, string email, string firstName, string lastName, bool isActive = true)
	{
		return new User
		{
			Id = id,
			Email = email,
			FirstName = firstName,
			LastName = lastName,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow.Date,
			Password = "Abc@12345",
			Cpf = "123.456.789-00",
			SignInWith = SignIn.Default.ToString(),
		};
	}

	// ----------------------------------------------------------------
	// GetByIdAsync (2 testes)
	// ----------------------------------------------------------------

	[Fact]
	public async Task GetByIdAsync_ReturnsUser_WhenExists()
	{
		var u1 = NewValidUser(10, "a@a.com", "Ana", "Silva");
		await _db.Users.AddRangeAsync(u1);
		await _db.SaveChangesAsync();

		var result = await _service.GetByIdAsync(10);

		result.Should().NotBeNull();
		result.Id.Should().Be(10);
		result.Email.Should().Be("a@a.com");
	}

	[Fact]
	public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
	{
		var result = await _service.GetByIdAsync(999);

		result.Should().BeNull();
	}

	// ----------------------------------------------------------------
	// GetAllAsync (2 testes)
	// ----------------------------------------------------------------

	[Fact]
	public async Task GetAllAsync_FiltersByIsActive_WhenProvided()
	{
		await _db.Users.AddRangeAsync(
			NewValidUser(1, "u1@x.com", "Bia", "A", isActive: true),
			NewValidUser(2, "u2@x.com", "Caio", "B", isActive: false),
			NewValidUser(3, "u3@x.com", "Duda", "C", isActive: true)
		);
		await _db.SaveChangesAsync();

		var filters = new UserFilters { IsActive = true };

		var result = await _service.GetAllAsync(filters);

		result.Count.Should().Be(2);
		result.Should().AllSatisfy(u => u.IsActive.Should().BeTrue());
	}

	[Fact]
	public async Task GetAllAsync_OrdersByFirstName_WhenOrderBy1()
	{
		await _db.Users.AddRangeAsync(
			NewValidUser(1, "z@x.com", "Zeca", "A"),
			NewValidUser(2, "a@x.com", "Ana", "B"),
			NewValidUser(3, "m@x.com", "Mia", "C")
		);
		await _db.SaveChangesAsync();

		var filters = new UserFilters { OrderBy = "1" }; // FirstName asc

		var result = await _service.GetAllAsync(filters);

		var expectedNames = new[] { "Ana", "Mia", "Zeca" };
		result.Select(s => s.FirstName).Should().Equal(expectedNames);
	}

	// ----------------------------------------------------------------
	// CreateAsync (2 testes)
	// ----------------------------------------------------------------

	[Fact]
	public async Task CreateAsync_ReturnsNull_AndNotifies_WhenValidationInvalid()
	{
		// Crie um usuário propositalmente inválido conforme suas regras de validação.
		// Ex: Email vazio costuma quebrar ValidateCreateAsync.
		var invalid = new User
		{
			Id = 1,
			Email = "",     //inválido
			FirstName = "", //inválido
			LastName = ""
		};

		var result = await _service.CreateAsync(invalid);

		result.Should().BeNull();

		// Ajuste a assinatura do AddNotifications conforme seu INotificationService real.
		_notificationMock.Verify(n => n.AddNotifications(It.IsAny<IList<Notification>>()), Times.Once);
	}

	[Fact]
	public async Task CreateAsync_CreatesUser_WhenValidAndEmailUnique()
	{
		var email = "new@x.com";
		var model = NewValidUser(0, email, "Joao", "Pereira");
		var result = await _service.CreateAsync(model);

		result.Should().NotBeNull();
		result.Email.Should().Be(email);

		var saved = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
		saved.Should().NotBeNull();
		saved.Email.Should().Be(email);
	}

	// ----------------------------------------------------------------
	// UpdateAsync (2 testes)
	// ----------------------------------------------------------------

	[Fact]
	public async Task UpdateAsync_ReturnsNull_WhenUserNotFound()
	{
		var model = NewValidUser(999, "x@x.com", "X", "Y");
		var result = await _service.UpdateAsync(model, 1, "Admin");
		result.Should().BeNull();
	}

	[Fact]
	public async Task UpdateAsync_UpdatesEntityAndCreatesLog_WhenEntityUpdated()
	{
		var model = NewValidUser(10, "u@x.com", "Ana", "Souza"); // mudou LastName (deve disparar EntityUpdated)
		await _service.CreateAsync(model);

		var result = await _service.UpdateAsync(model, loggedUserId: 77, loggedUserName: "Manager");

		result.Should().NotBeNull();
		result.LastName.Should().Be("Souza");
		result.UpdatedBy.Should().Be("Manager");

		// log criado pelo UpdateAsync (via CreateUserLogAsync)
		var logs = await _db.UserLogs.Where(w => w.UserId == 77).ToListAsync();
		logs.Count.Should().BeGreaterThanOrEqualTo(1);
		logs.Should().Contain(l => l.Log.Contains("User Id 10 Updated"));
	}

	// ----------------------------------------------------------------
	// PatchAsync (2 testes)
	// ----------------------------------------------------------------

	[Fact]
	public async Task PatchAsync_ReturnsNull_WhenUserNotFound()
	{
		var model = NewValidUser(404, "p@x.com", "A", "B");
		var result = await _service.PatchAsync(model, loggedUserId: 1, loggedUserName: "Admin");
		result.Should().BeNull();
	}

	[Fact]
	public async Task PatchAsync_UpdatesEntityAndCreatesLog_WhenEntityUpdated()
	{
		var existing = NewValidUser(20, "patch@x.com", "Carlos", "Lima");
		await _service.CreateAsync(existing);

		var model = NewValidUser(20, "patch@x.com", "Carla", "Lima"); // mudou FirstName
		var result = await _service.PatchAsync(model, loggedUserId: 99, loggedUserName: "Supervisor");

		result.Should().NotBeNull();
		result.FirstName.Should().Be("Carla");
		result.UpdatedBy.Should().Be("Supervisor");

		var logs = await _db.UserLogs.Where(w => w.UserId == 99).ToListAsync();
		logs.Count.Should().BeGreaterThanOrEqualTo(1);
		logs.Should().Contain(l => l.Log.Contains("User Id 20 Updated"));
	}

	// ----------------------------------------------------------------
	// DeleteAsync (2 testes)
	// ----------------------------------------------------------------

	[Fact]
	public async Task DeleteAsync_ReturnsNull_WhenUserNotFound()
	{
		var result = await _service.DeleteAsync(id: 123, loggedUserId: 1, loggedUserName: "Admin");
		result.Should().BeNull();
	}

	[Fact]
	public async Task DeleteAsync_SoftDeletesAndCreatesLog_WhenUserExists()
	{
		var existing = NewValidUser(30, "del@x.com", "Mario", "Nunes", isActive: true);
		await _service.CreateAsync(existing);

		var result = await _service.DeleteAsync(id: 30, loggedUserId: 7, loggedUserName: "Boss");
		result.Should().BeTrue();

		var saved = await _service.GetByIdAsync(30);
		saved.Should().NotBeNull();
		saved.IsActive.Should().BeFalse();
		saved.DeletedAt.Should().NotBeNull();
		saved.UpdatedBy.Should().Be("Boss");

		var logs = await _db.UserLogs.Where(w => w.UserId == 7).ToListAsync();
		logs.Count.Should().BeGreaterThanOrEqualTo(1);
		logs.Should().Contain(l => l.Log.Contains("User Id 30 Deleted"));
	}

	// ----------------------------------------------------------------
	// SetAcceptedTermsAt (2 testes)
	// ----------------------------------------------------------------

	[Fact]
	public async Task SetAcceptedTermsAt_DoesNothing_WhenUserNotFound()
	{
		// não deve lançar exception
		await _service.SetAcceptedTermsAt(999);

		_notificationMock.Verify(n => n.AddNotifications(It.IsAny<IList<Notification>>()), Times.Never);
	}

	[Fact]
	public async Task SetAcceptedTermsAt_SetsAcceptedTermsAt_WhenUserExists()
	{
		var existing = NewValidUser(40, "terms@x.com", "Lia", "Dias");
		await _service.CreateAsync(existing);

		await _service.SetAcceptedTermsAt(40);

		var saved = await _service.GetByIdAsync(40);
		saved.Should().NotBeNull();
		saved.AcceptedTermsAt.Should().NotBeNull();
	}

	// ----------------------------------------------------------------
	// CreateUserLogAsync (2 testes)
	// ----------------------------------------------------------------

	[Fact]
	public async Task CreateUserLogAsync_ReturnsNull_AndNotifies_WhenValidationInvalid()
	{
		// Monte um log inválido conforme suas regras.
		// Ex: descrição vazia geralmente invalida.
		var invalidLog = new UserLog(1, "");
		var result = await _service.CreateUserLogAsync(invalidLog);
		result.Should().BeNull();

		// Ajuste assinatura conforme seu INotificationService real
		_notificationMock.Verify(n => n.AddNotifications(It.IsAny<IList<Notification>>()), Times.Once);
	}

	[Fact]
	public async Task CreateUserLogAsync_CreatesLog_WhenValid()
	{
		var log = new UserLog(123, "User Id 123 Something");

		var result = await _service.CreateUserLogAsync(log);
		result.Should().NotBeNull();
		result.UserId.Should().Be(123);

		var logs = await _db.UserLogs.Where(w => w.UserId == 123).ToListAsync();
		logs.Count.Should().BeGreaterThanOrEqualTo(1);
	}
}
