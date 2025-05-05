using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FileManagementSystem.Models;


namespace FileManagementSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

          protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=FileManagement.db");
            }
        }
            public DbSet<FileModel> Files { get; set; } 
            public DbSet<Label> Labels { get; set; }
            public DbSet<FileLabel> FileLabels { get; set; }
            public DbSet<FolderModel> Folders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FileLabel>()
                .HasKey(fl => new { fl.FileId, fl.LabelId });

            modelBuilder.Entity<FileLabel>()
                .HasOne(fl => fl.File)
                .WithMany(f => f.FileLabels)
                .HasForeignKey(fl => fl.FileId);

            modelBuilder.Entity<FileLabel>()
                .HasOne(fl => fl.Label)
                .WithMany(l => l.FileLabels)
                .HasForeignKey(fl => fl.LabelId);

            // Folder relationships
            modelBuilder.Entity<FolderModel>()
                .HasMany(f => f.SubFolders)
                .WithOne(f => f.ParentFolder)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FolderModel>()
                .HasMany(f => f.Files)
                .WithOne(f => f.Folder)
                .HasForeignKey(f => f.FolderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
