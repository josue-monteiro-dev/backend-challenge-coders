namespace Api.Data.Context;

public class CnabDbContext(DbContextOptions<CnabDbContext> o) : DbContext(o)
{
	public DbSet<Email> Emails => Set<Email>();
	public DbSet<User> Users => Set<User>();
	public DbSet<UserLog> UserLogs => Set<UserLog>();
	public DbSet<TransactionType> TransactionTypes => Set<TransactionType>();
	public DbSet<Transaction> Transactions => Set<Transaction>();

	protected override void OnConfiguring(DbContextOptionsBuilder o)
	{
		o.UseSqlite("Data Source=CnabImporter.db;Cache=Shared");
	}

	protected override void OnModelCreating(ModelBuilder b)
	{
		b.Entity<Email>(entity =>
		{
			entity.ToTable("emails");
			entity.Property(e => e.Id).HasColumnName("id");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.IsActive).IsRequired().HasColumnType("boolean").HasColumnName("is_active");
			entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("timestamp").HasColumnName("created_at");
			entity.Property(e => e.UpdatedAt).HasColumnType("timestamp").HasColumnName("updated_at");
			entity.Property(e => e.DeletedAt).HasColumnType("timestamp").HasColumnName("deleted_at");
			entity.Property(u => u.UpdatedBy).HasColumnType("varchar(50)").HasColumnName("updated_by");

			entity.Property(e => e.UserId).HasColumnType("bigint").HasColumnName("user_id");
			entity.Property(u => u.BookId).HasColumnType("bigint").HasColumnName("book_id");
			entity.Property(e => e.EmailType).HasColumnType("smallint").HasColumnName("email_type");
			entity.Property(e => e.ScheduleDate).HasColumnType("timestamp").HasColumnName("schedule_date");
			entity.Property(e => e.DateSent).HasColumnType("timestamp").HasColumnName("date_sent");
			entity.Property(e => e.SendAttempts).HasColumnType("smallint").HasColumnName("send_attempts");

			entity.HasOne(e => e.User).WithMany(e => e.Emails).HasForeignKey(e => e.UserId).IsRequired().OnDelete(DeleteBehavior.Restrict);
		});

		b.Entity<User>(entity =>
		{
			entity.ToTable("users");
			entity.Property(u => u.Id).HasColumnName("id");
			entity.HasKey(u => u.Id);
			entity.Property(u => u.IsActive).IsRequired().HasColumnType("boolean").HasColumnName("is_active");
			entity.Property(u => u.CreatedAt).IsRequired().HasColumnType("timestamp").HasColumnName("created_at");
			entity.Property(u => u.UpdatedAt).HasColumnType("timestamp").HasColumnName("updated_at");
			entity.Property(u => u.DeletedAt).HasColumnType("timestamp").HasColumnName("deleted_at");
			entity.Property(u => u.UpdatedBy).HasColumnType("varchar(50)").HasColumnName("updated_by");

			entity.Property(u => u.FirstName).HasColumnType("varchar(50)").HasColumnName("first_name");
			entity.Property(u => u.LastName).HasColumnType("varchar(50)").HasColumnName("last_name");
			entity.Property(u => u.Email).IsRequired().HasColumnType("varchar(100)").HasColumnName("email");
			entity.HasIndex(u => u.Email).IsUnique();
			entity.Property(o => o.Cpf).HasColumnType("varchar(50)").HasColumnName("cpf");
			entity.Property(u => u.SignInWith).IsRequired().HasColumnType("varchar(10)").HasColumnName("sign_in_with");
			entity.Property(o => o.Type).HasColumnType("smallint").HasColumnName("type");
			entity.Property(u => u.BirthDate).HasColumnType("date").HasColumnName("birth_date");
			entity.Property(u => u.ProfileImgUrl).HasColumnType("text").HasColumnName("profile-img-url");
			entity.Property(u => u.PasswordHash).HasColumnName("password_hash");
			entity.Property(u => u.ActivationCode).HasColumnType("varchar(50)").HasColumnName("activation_code");
			entity.Property(u => u.ActivationAt).HasColumnType("timestamp").HasColumnName("activation_at");
			entity.Property(u => u.ResetPassword).HasColumnType("boolean").HasColumnName("reset_password");
			entity.Property(u => u.ResetPasswordCode).HasColumnType("varchar(50)").HasColumnName("reset_password_code");
			entity.Property(u => u.ResetPasswordAt).HasColumnType("timestamp").HasColumnName("reset_password_at");
			entity.Property(u => u.AcceptedTermsAt).HasColumnType("timestamp").HasColumnName("accepted_terms_at");
		});

		b.Entity<UserLog>(entity =>
		{
			entity.ToTable("user_logs");
			entity.Property(u => u.Id).HasColumnName("id");
			entity.HasKey(u => u.Id);
			entity.Property(u => u.CreatedAt).IsRequired().HasColumnType("timestamp").HasColumnName("created_at");

			entity.Property(u => u.UserId).HasColumnType("bigint").HasColumnName("user_id");
			entity.Property(u => u.Log).IsRequired().HasColumnType("varchar(100)").HasColumnName("log");

			entity.HasOne(u => u.User).WithMany(u => u.UserLogs).HasForeignKey(u => u.UserId).IsRequired().OnDelete(DeleteBehavior.Restrict);
		});

		b.Entity<TransactionType>(entity =>
		{
			entity.ToTable("transaction_type");
			entity.Property(u => u.Id).HasColumnName("id");
			entity.HasKey(u => u.Id);
			entity.Property(u => u.CreatedAt).IsRequired().HasColumnType("timestamp").HasColumnName("created_at");

			entity.Property(o => o.Type).HasColumnType("smallint").HasColumnName("type");
			entity.Property(u => u.Description).IsRequired().HasColumnType("varchar(20)").HasColumnName("description");
			entity.Property(u => u.Nature).IsRequired().HasColumnType("varchar(20)").HasColumnName("nature");
			entity.Property(u => u.Sign).IsRequired().HasColumnType("varchar(2)").HasColumnName("sign");
		});

		b.Entity<Transaction>(entity =>
		{
			entity.ToTable("transaction");
			entity.Property(u => u.Id).HasColumnName("id");
			entity.HasKey(u => u.Id);
			entity.Property(u => u.CreatedAt).IsRequired().HasColumnType("timestamp").HasColumnName("created_at");

			entity.Property(u => u.Date).HasColumnType("date").HasColumnName("date");
			entity.Property(v => v.Value).HasColumnType("decimal(10,2)").HasColumnName("value");
			entity.Property(o => o.Cpf).HasColumnType("varchar(11)").HasColumnName("cpf");
			entity.Property(o => o.Card).HasColumnType("varchar(12)").HasColumnName("card");
			entity.Property(u => u.Time).HasColumnType("time").HasColumnName("time");
			entity.Property(o => o.Owner).HasColumnType("varchar(14)").HasColumnName("owner");
			entity.Property(o => o.Store).HasColumnType("varchar(19)").HasColumnName("store");
			entity.Property(u => u.TransactionTypeId).HasColumnType("bigint").HasColumnName("transaction_type_id");

			entity.HasOne(u => u.TransactionType).WithMany(u => u.Transactions).HasForeignKey(u => u.TransactionTypeId).IsRequired().OnDelete(DeleteBehavior.Restrict);
		});
	}
}
