namespace Api.Services;

public interface ITransactionTypeService
{
    Task<TransactionType?> GetByIdAsync(long id);
	Task<List<TransactionType>> GetAllAsync(TransactionTypeFilters filters);
	Task<TransactionType?> CreateAsync(TransactionType model);
	Task<TransactionType?> UpdateAsync(TransactionType model, string loggedUserName);
	Task<TransactionType?> PatchAsync(TransactionType model, string loggedUserName);
	Task<bool?> DeleteAsync(long id, string loggedUserName);
}

public sealed class TransactionTypeService(
	CnabDbContext db,
    INotificationService notification) : ITransactionTypeService
{
    public async Task<TransactionType?> GetByIdAsync(long id) => await db.TransactionTypes.FirstOrDefaultAsync(f => f.Id == id);

	public async Task<List<TransactionType>> GetAllAsync(TransactionTypeFilters filters)
	{
		var predicate = PredicateBuilder.New<TransactionType>(true);

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
				EF.Functions.Like(a.Description, filters.Filter.LikeConcat()) ||
				EF.Functions.Like(a.Nature, filters.Filter.LikeConcat()) ||
				EF.Functions.Like(a.Sign, filters.Filter.LikeConcat())
			);
		}

		if (!string.IsNullOrEmpty(filters.Description))
			predicate.And(a => EF.Functions.Like(a.Description, filters.Description.LikeConcat()));

		if (!string.IsNullOrEmpty(filters.Nature))
			predicate.And(a => EF.Functions.Like(a.Nature, filters.Nature.LikeConcat()));

		if (!string.IsNullOrEmpty(filters.Sign))
			predicate.And(a => EF.Functions.Like(a.Sign, filters.Sign.LikeConcat()));

		if (filters.Type.HasValue)
			predicate.And(a => a.Type == filters.Type);

		#endregion

		var query = db.TransactionTypes.Where(predicate);

		#region OrderBy

		query = filters?.OrderBy switch
		{
			"1" => query.OrderBy(o => o.Type),
			"2" => query.OrderBy(o => o.Description),
			_ => query.OrderBy(o => o.Id)
		};

		#endregion

#if DEBUG
		var queryString = query.ToQueryString();
#endif

		return await query.ToListAsync();
	}

	public async Task<TransactionType?> CreateAsync(TransactionType model)
	{
		var validation = await model.ValidateCreateAsync();
		if (!validation.IsValid)
		{
			notification.AddNotifications(validation);
			return default;
		}

		var addResult = await db.TransactionTypes.AddAsync(model);
		await db.SaveChangesAsync();

		return addResult.Entity;
	}

	public async Task<TransactionType?> UpdateAsync(TransactionType model, string loggedUserName)
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
			db.TransactionTypes.Update(entitie);
			await db.SaveChangesAsync();
		}

		return entitie;
	}

	public async Task<TransactionType?> PatchAsync(TransactionType model, string loggedUserName)
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
			db.TransactionTypes.Update(entitie);
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

		db.TransactionTypes.Update(entitie);
		await db.SaveChangesAsync();

		return true;
	}
}
