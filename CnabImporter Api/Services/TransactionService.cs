namespace Api.Services;

public interface ITransactionService
{
    Task<Transaction?> GetByIdAsync(long id);
	Task<List<Transaction>> GetAllAsync(TransactionFilters filters);
	Task<Transaction?> CreateAsync(Transaction model, long loggedUserId);
	Task<Transaction?> UpdateAsync(Transaction model, string loggedUserName);
	Task<Transaction?> PatchAsync(Transaction model, string loggedUserName);
	Task<bool?> DeleteAsync(long id, string loggedUserName);
	Task<bool?> UploadFileWithTransactions(IFormFile file, long loggedUserId, string loggedUserName);
}

public sealed class TransactionService(
	CnabDbContext db,
	ILogger<TransactionService> logger,
	INotificationService notification) : ITransactionService
{
    public async Task<Transaction?> GetByIdAsync(long id) => await db.Transactions.FirstOrDefaultAsync(f => f.Id == id);

	public async Task<List<Transaction>> GetAllAsync(TransactionFilters filters)
	{
		var predicate = PredicateBuilder.New<Transaction>(true);

		#region Filters

		if (filters.Id.HasValue)
			predicate.And(a => a.Id == filters.Id);

		if (filters.IsActive.HasValue)
			predicate.And(a => a.IsActive == filters.IsActive);

		if (filters.CreatedAt.HasValue && filters.CreatedAt > DateTime.MinValue)
			predicate.And(a => a.CreatedAt == filters.CreatedAt.Value.Date);

		if (filters.UpdatedAt.HasValue && filters.UpdatedAt > DateTime.MinValue)
			predicate.And(a => a.UpdatedAt == filters.UpdatedAt.Value.Date);

		if (filters.DeletedAt.HasValue && filters.DeletedAt > DateTime.MinValue)
			predicate.And(a => a.DeletedAt == filters.DeletedAt.Value.Date);

		if (!string.IsNullOrEmpty(filters.Filter))
		{
			predicate.And(a =>
				EF.Functions.Like(a.Cpf, filters.Filter.LikeConcat()) ||
				EF.Functions.Like(a.Card, filters.Filter.LikeConcat())
			);
		}

		if (!string.IsNullOrEmpty(filters.Cpf))
			predicate.And(a => EF.Functions.Like(a.Cpf, filters.Cpf.LikeConcat()));

		if (!string.IsNullOrEmpty(filters.Card))
			predicate.And(a => EF.Functions.Like(a.Card, filters.Card.LikeConcat()));

		if (filters.Date.HasValue && filters.Date > DateTime.MinValue)
			predicate.And(a => a.Date == filters.Date.Value.Date);

		if (filters.Time.HasValue && filters.Time > TimeSpan.MinValue)
			predicate.And(a => a.Time == filters.Time.Value);

		if (filters.TransactionTypeId.HasValue)
			predicate.And(a => a.TransactionTypeId == filters.TransactionTypeId);

		#endregion

		var query = db.Transactions.Where(predicate);

		#region OrderBy

		query = filters?.OrderBy switch
		{
			"1" => query.OrderBy(o => o.Cpf),
			"2" => query.OrderBy(o => o.Card),
			_ => query.OrderBy(o => o.Id)
		};

		#endregion

#if DEBUG
		var queryString = query.ToQueryString();
#endif

		return await query.ToListAsync();
	}

	public async Task<Transaction?> CreateAsync(Transaction model, long loggedUserId)
	{
		var validation = await model.ValidateCreateAsync();
		if (!validation.IsValid)
		{
			notification.AddNotifications(validation);
			return default;
		}

		var transactionTypeExists = await db.TransactionTypes.AnyAsync(a => a.Id == model.TransactionTypeId && a.IsActive);
		if (!transactionTypeExists)
		{
			notification.AddNotification("TransactionType", "Transaction Type invalid.");
			return default;
		}

		model.UserId = loggedUserId;

		var addResult = await db.Transactions.AddAsync(model);
		await db.SaveChangesAsync();

		return addResult.Entity;
	}

	public async Task<Transaction?> UpdateAsync(Transaction model, string loggedUserName)
	{
		var validation = await model.ValidateUpdateAsync();
		if (!validation.IsValid)
		{
			notification.AddNotifications(validation);
			return default;
		}

		var entitie = await GetByIdAsync(model.Id);
		if (entitie == null) return null;

		if (entitie.EntityUpdated(model))
		{
			entitie.UpdatedAt = DateTimeBr.Now;
			entitie.UpdatedBy = loggedUserName;
			db.Transactions.Update(entitie);
			await db.SaveChangesAsync();
		}

		return entitie;
	}

	public async Task<Transaction?> PatchAsync(Transaction model, string loggedUserName)
	{
		var validation = await model.ValidatePatchAsync();
		if (!validation.IsValid)
		{
			notification.AddNotifications(validation);
			return default;
		}

		var entitie = await GetByIdAsync(model.Id);
		if (entitie == null) return null;

		if (entitie.EntityUpdated(model))
		{
			entitie.UpdatedAt = DateTimeBr.Now;
			entitie.UpdatedBy = loggedUserName;
			db.Transactions.Update(entitie);
			await db.SaveChangesAsync();
		}

		return entitie;
	}

	public async Task<bool?> DeleteAsync(long id, string loggedUserName)
	{
		var entitie = await GetByIdAsync(id);
		if (entitie == null) return null;

		entitie.IsActive = false;
		entitie.DeletedAt = DateTimeBr.Now;
		entitie.UpdatedBy = loggedUserName;

		db.Transactions.Update(entitie);
		await db.SaveChangesAsync();

		return true;
	}

	public async Task<bool?> UploadFileWithTransactions(IFormFile file, long loggedUserId, string loggedUserName)
	{
		if (file == null || file.Length == 0)
		{
			notification.AddNotification("UploadFileWithTransactions", "File empty.");
			return false;
		}

		var transactions = new List<Transaction>();
		var transactionTypes = await db.TransactionTypes.ToDictionaryAsync(k => k.Id, v => v.Type);

		logger.LogInformation("Starting to read the file with transactions.");

		using var stream = file.OpenReadStream();
		using var reader = new StreamReader(stream);

		while (!reader.EndOfStream)
		{
			var line = await reader.ReadLineAsync();

			if (string.IsNullOrWhiteSpace(line) || line.Length < 81)
			{
				logger.LogWarning("Skipping invalid or empty line: {Line}", line);
				continue;
			}

			logger.LogInformation("Processing line: {Line}", line);

			try
			{
				var type = int.Parse(line.Substring(0, 1));
				if (!transactionTypes.ContainsValue(type))
				{
					var errorMsg = $"Invalid transaction type '{type}' in line: {line}";
					logger.LogError(errorMsg);
					notification.AddNotification("UploadFileWithTransactions - reading type", errorMsg);
					continue;
				}

				var date = DateTime.ParseExact(
					line.Substring(1, 8),
					"yyyyMMdd",
					CultureInfo.InvariantCulture);

				var time = TimeSpan.ParseExact(
					line.Substring(42, 6),
					"hhmmss",
					CultureInfo.InvariantCulture);

				var amount = decimal.Parse(line.Substring(9, 10)) / 100m;

				var transaction = new Transaction
				{
					Date = date,
					Time = time,
					Value = amount,
					Cpf = line.Substring(19, 11).Trim(),
					Card = line.Substring(30, 12).Trim(),
					Owner = line.Substring(48, 14).Trim(),
					Store = line.Substring(62, 19).Trim(),

					//Get ID from dictionary
					TransactionTypeId = transactionTypes.FirstOrDefault(f => f.Value == type).Key,

					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow,
					UpdatedBy = loggedUserName,
				};

				transactions.Add(transaction);
			}
			catch (Exception ex)
			{
				var errorMsg = $"Error parsing line: {line}";
				logger.LogError(ex, "{ErrorMsg}", errorMsg);
				notification.AddNotification("UploadFileWithTransactions - reading line", errorMsg);
			}

			logger.LogInformation("Finished processing line: {Line}", line);
		}

		if (transactions.Count == 0)
		{
			var msg = "No transactions read.";
			logger.LogInformation(msg);
			notification.AddNotification("UploadFileWithTransactions - finish reading lines", msg);
			return false;
		}

		using var transactionScope = await db.Database.BeginTransactionAsync();
		logger.LogInformation("Saving {Count} transactions to the database.", transactions.Count);

		try
		{
			db.Transactions.AddRange(transactions);
			await db.SaveChangesAsync();
			await transactionScope.CommitAsync();

			logger.LogInformation("Transactions saved successfully.");
		}
		catch (Exception ex)
		{
			await transactionScope.RollbackAsync();
			
			var errorMsg = "Error saving transactions.";
			logger.LogError(ex, errorMsg);
			notification.AddNotification("UploadFileWithTransactions - reading line", errorMsg);
			return false;
		}

		await db.UserLogs.AddAsync(new UserLog(loggedUserId, string.Format("User Id {0} uploaded file {1}.", loggedUserId, file.Name)));
		
		return true;
	}
}
