using Api.Data.Context;
using Api.Data.Dtos;
using Api.Data.Entities;
using Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using Xunit;

namespace CnabImporter.Tests.Services;

public class TransactionServiceTests
{
	protected readonly CnabDbContext _db;
	protected readonly Mock<INotificationService> _notificationMock;
	protected readonly Mock<ILogger<TransactionService>> _loggerMock;
	protected readonly TransactionService _service;

	public TransactionServiceTests()
	{
		var options = new DbContextOptionsBuilder<CnabDbContext>()
			.UseSqlite("Filename=:memory:")
			.EnableSensitiveDataLogging()
			.Options;

		_db = new CnabDbContext(options);
		_db.Database.EnsureCreated();

		_notificationMock = new Mock<INotificationService>();
		_loggerMock = new Mock<ILogger<TransactionService>>();

		_service = new TransactionService(
			_db,
			_loggerMock.Object,
			_notificationMock.Object
		);
	}

	[Fact]
	public async Task GetByIdAsync_ReturnsTransaction_WhenExists()
	{
		var transactionType = new TransactionType
		{
			Id = 1,
			Type = 1,
			Description = "New Type",
			Nature = "Income",
			Sign = "+"
		};
		await _db.TransactionTypes.AddAsync(transactionType);
		await _db.SaveChangesAsync();

		var transaction = new Transaction
		{
			Id = 1,
			Date = DateTime.Now,
			Value = 100,
			Cpf = "12345678901",
			Card = "1234",
			Time = DateTime.Now.TimeOfDay,
			Owner = "Owner Name",
			Store = "Store Name",
			TransactionTypeId = 1
		};

		await _service.CreateAsync(transaction, 1);

		var result = await _service.GetByIdAsync(1);

		result.Should().NotBeNull();
		result!.Id.Should().Be(1);
	}

	[Fact]
	public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _service.GetByIdAsync(999);

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetAllAsync_ReturnsAllTransactions()
	{
		_db.Transactions.AddRange(
			new Transaction { Cpf = "111" },
			new Transaction { Cpf = "222" }
		);
		await _db.SaveChangesAsync();

		var result = await _service.GetAllAsync(new TransactionFilters());

		result.Should().HaveCount(2);
	}

	[Fact]
	public async Task GetAllAsync_FiltersByCpf()
	{
		_db.Transactions.AddRange(
			new Transaction { Cpf = "111" },
			new Transaction { Cpf = "222" }
		);
		await _db.SaveChangesAsync();

		var filters = new TransactionFilters { Cpf = "111" };

		var result = await _service.GetAllAsync(filters);

		result.Should().HaveCount(1);
		result.First().Cpf.Should().Be("111");
	}

	[Fact]
	public async Task CreateAsync_ReturnsTransaction_WhenValid()
	{
		_db.TransactionTypes.Add(new TransactionType { Id = 1, IsActive = true });
		await _db.SaveChangesAsync();

		var transaction = new Transaction
		{
			TransactionTypeId = 1,
			Value = 100
		};

		var result = await _service.CreateAsync(transaction, 1);

		result.Should().NotBeNull();
		_db.Transactions.Should().HaveCount(1);
	}

	[Fact]
	public async Task CreateAsync_ReturnsNull_WhenTransactionTypeInvalid()
	{
		var transaction = new Transaction { TransactionTypeId = 999 };

		var result = await _service.CreateAsync(transaction, 1);

		result.Should().BeNull();
		_notificationMock.Verify(n =>
			n.AddNotification("TransactionType", "Transaction Type invalid."),
			Times.Once);
	}

	[Fact]
	public async Task UpdateAsync_UpdatesTransaction_WhenExists()
	{
		var transaction = new Transaction { Id = 1, Card = "OLD" };
		_db.Transactions.Add(transaction);
		await _db.SaveChangesAsync();

		transaction.Card = "NEW";

		var result = await _service.UpdateAsync(transaction, "admin");

		result.Should().NotBeNull();
		result!.Card.Should().Be("NEW");
		result.UpdatedBy.Should().Be("admin");
	}

	[Fact]
	public async Task UpdateAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _service.UpdateAsync(new Transaction { Id = 999 }, "admin");

		result.Should().BeNull();
	}

	[Fact]
	public async Task PatchAsync_PatchesTransaction()
	{
		var transaction = new Transaction { Id = 1, Store = "Old" };
		_db.Transactions.Add(transaction);
		await _db.SaveChangesAsync();

		transaction.Store = "New";

		var result = await _service.PatchAsync(transaction, "admin");

		result.Should().NotBeNull();
		result!.Store.Should().Be("New");
	}

	[Fact]
	public async Task PatchAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _service.PatchAsync(new Transaction { Id = 999 }, "admin");

		result.Should().BeNull();
	}

	[Fact]
	public async Task DeleteAsync_DeactivatesTransaction()
	{
		var transaction = new Transaction { Id = 1, IsActive = true };
		_db.Transactions.Add(transaction);
		await _db.SaveChangesAsync();

		var result = await _service.DeleteAsync(1, "admin");

		result.Should().BeTrue();
		transaction.IsActive.Should().BeFalse();
		transaction.DeletedAt.Should().NotBeNull();
	}

	[Fact]
	public async Task DeleteAsync_ReturnsNull_WhenNotFound()
	{
		var result = await _service.DeleteAsync(999, "admin");

		result.Should().BeNull();
	}

	private static IFormFile CreateFile(string content)
	{
		var bytes = Encoding.UTF8.GetBytes(content);
		var stream = new MemoryStream(bytes);

		return new FormFile(stream, 0, bytes.Length, "file", "transactions.txt");
	}

	[Fact]
	public async Task UploadFile_ReturnsFalse_WhenFileIsEmpty()
	{
		var file = CreateFile(string.Empty);

		var result = await _service.UploadFileWithTransactions(file, 1, "admin");

		result.Should().BeFalse();

		_notificationMock.Verify(n =>
			n.AddNotification("UploadFileWithTransactions", "File empty."),
			Times.Once);
	}

	[Fact]
	public async Task UploadFile_ReturnsTrue_WhenValidFile()
	{
		_db.TransactionTypes.Add(new TransactionType { Id = 1, Type = 1, IsActive = true });
		await _db.SaveChangesAsync();

		var line = "120230101000000100012345678901234567890123450930001OWNER NAME   STORE NAME         ";

		var file = CreateFile(line);

		var result = await _service.UploadFileWithTransactions(file, 1, "admin");
		result.Should().BeTrue();

		var lastTransactionInserted = await _db.Transactions.OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();
		lastTransactionInserted.Should().NotBeNull();
	}
}
