using Api.Data.Context;
using Api.Data.Dtos;
using Api.Data.Entities;
using Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CnabImporter.Tests.Services;

public class TransactionTypeServiceTest
{
	protected readonly CnabDbContext _db;
	protected readonly Mock<INotificationService> _notificationMock;
	protected readonly TransactionTypeService _service;

	public TransactionTypeServiceTest()
	{
		var options = new DbContextOptionsBuilder<CnabDbContext>()
			.UseSqlite("Data Source=CnabImporterTests.db;Cache=Shared")
			.EnableSensitiveDataLogging()
			.Options;

		_db = new CnabDbContext(options);
		_db.Database.EnsureDeleted();
		_db.Database.EnsureCreated();

		_notificationMock = new Mock<INotificationService>();

		_service = new TransactionTypeService(
			_db,
			_notificationMock.Object
		);
	}

	[Fact]
	public async Task GetByIdAsync_Returns_WhenExists()
	{
		var transactionType = new TransactionType
		{
			Id = 10,
			Type = 1,
			Description = "New Type",
			Nature = "Income",
			Sign = "+"
		};

		await _service.CreateAsync(transactionType);

		var result = await _service.GetByIdAsync(10);

		result.Should().NotBeNull();
		result.Id.Should().Be(10);
		result.Type.Should().Be(1);
		result.Description.Should().Be("New Type");
		result.Nature.Should().Be("Income");
		result.Sign.Should().Be("+");
	}

	[Fact]
	public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
	{
		var result = await _service.GetByIdAsync(999);

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetAllAsync_ReturnsAll_WhenNoFilters()
	{
		await _service.CreateAsync(new TransactionType
		{
			Type = 1,
			Description = "Credit",
			Nature = "Income",
			Sign = "+"
		});

		await _service.CreateAsync(new TransactionType
		{
			Type = 2,
			Description = "Debit",
			Nature = "Outcome",
			Sign = "-"
		});

		var result = await _service.GetAllAsync(new TransactionTypeFilters());

		result.Should().HaveCount(2);
	}

	[Fact]
	public async Task GetAllAsync_FiltersByType()
	{
		await _service.CreateAsync(new TransactionType
		{
			Type = 1,
			Description = "Credit",
			Nature = "Income",
			Sign = "+"
		});

		await _service.CreateAsync(new TransactionType
		{
			Type = 2,
			Description = "Debit",
			Nature = "Outcome",
			Sign = "-"
		});

		var filters = new TransactionTypeFilters { Type = 1 };

		var result = await _service.GetAllAsync(filters);

		result.Should().HaveCount(1);
		result.First().Type.Should().Be(1);
	}

	[Fact]
	public async Task CreateAsync_ReturnsTransactionType_WhenValid()
	{
		var model = new TransactionType
		{
			Type = 3,
			Description = "Pix",
			Nature = "Income",
			Sign = "+"
		};

		var result = await _service.CreateAsync(model);

		result.Should().NotBeNull();
		result!.Id.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task CreateAsync_ReturnsNull_AndNotifies_WhenInvalid()
	{
		var invalid = new TransactionType
		{
			Type = 0,
			Description = "",
			Nature = "",
			Sign = ""
		};

		var result = await _service.CreateAsync(invalid);

		result.Should().BeNull();
		_notificationMock.Verify(
			n => n.AddNotifications(It.IsAny<IList<Notification>>()),
			Times.Once);
	}

	[Fact]
	public async Task UpdateAsync_UpdatesEntity_WhenExists()
	{
		var entity = await _service.CreateAsync(new TransactionType
		{
			Type = 1,
			Description = "Old",
			Nature = "Income",
			Sign = "+"
		});

		entity!.Description = "Updated";

		var result = await _service.UpdateAsync(entity, "admin");

		result.Should().NotBeNull();
		result!.Description.Should().Be("Updated");
		result.UpdatedBy.Should().Be("admin");
	}

	[Fact]
	public async Task UpdateAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _service.UpdateAsync(
			new TransactionType { Id = 999 },
			"admin");

		result.Should().BeNull();
	}

	[Fact]
	public async Task PatchAsync_PatchesEntity_WhenExists()
	{
		var entity = await _service.CreateAsync(new TransactionType
		{
			Type = 5,
			Description = "Initial",
			Nature = "Income",
			Sign = "+"
		});

		entity!.Description = "Patched";

		var result = await _service.PatchAsync(entity, "admin");

		result.Should().NotBeNull();
		result!.Description.Should().Be("Patched");
	}

	[Fact]
	public async Task PatchAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _service.PatchAsync(new TransactionType { Id = 999 }, "admin");

		result.Should().BeNull();
	}

	[Fact]
	public async Task DeleteAsync_DeactivatesEntity()
	{
		var entity = await _service.CreateAsync(new TransactionType
		{
			Type = 9,
			Description = "Delete me",
			Nature = "Outcome",
			Sign = "-"
		});

		var result = await _service.DeleteAsync(entity!.Id, "admin");

		result.Should().BeTrue();

		var deleted = await _service.GetByIdAsync(entity.Id);
		deleted!.IsActive.Should().BeFalse();
		deleted.DeletedAt.Should().NotBeNull();
		deleted.UpdatedBy.Should().Be("admin");
	}

	[Fact]
	public async Task DeleteAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _service.DeleteAsync(999, "admin");

		result.Should().BeNull();
	}
}
