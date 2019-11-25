using System;
using System.Data.Common;
using System.Data.Entity;
using System.Data.SQLite;
using System.IO;

using System.Data.Entity.Core.Common;
using System.Data.SQLite.EF6;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;

using System.Linq;
using System.Reflection;

using System.Collections.Generic;

namespace dbtest
{
    public class ApplicationDbContext : DbContext
    {
        static private string s_migrationSqlitePath;
        static ApplicationDbContext()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var exeDirInfo = new DirectoryInfo(exeDir);
            var projectDir = exeDirInfo.Parent.Parent.FullName;
            s_migrationSqlitePath = $@"{projectDir}\MigrationDb.sqlite3";
            // s_migrationSqlitePath = $@":memory:";
        }

        // enable-migrations 用なのでプロジェクトパスに MigrationDb.sqlite3 として出力する
        // アプリケーション用のインターフェースでない。
        public ApplicationDbContext() : base(new SQLiteConnection($"DATA Source={s_migrationSqlitePath}"), false)
        {
        }

        // アプリケーション用 DB名指定用
        public ApplicationDbContext(string name) : base(new SQLiteConnection($"DATA Source={name}"), true)
        {
        }

        // アプリケーション用 DBコネクション指定用
        public ApplicationDbContext(DbConnection connection) : base(connection, true)
        {
        }

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Category> Categories { get; set; }
    }

    // テーブル名は複数形のBlogs
    [Table("Blogs")]
    public class Blog
    {
        // 主キー設定
        [Key]
        public long Id { get; set; }
        // 必須
        [Required]
        public string Name { get; set; }
        public virtual ICollection<Post> Posts { get; set; } // has_many

        public Blog()
        {
        }

        public Blog(long id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    [Table("Posts")]
    public class Post
    {
        // 主キー設定
        [Key]
        public int Id { get; set; }
        // 必須
        [Required]
        public string Title { get; set; }
        //public string Content { get; set; }

        public virtual Blog Blog { get; set; } // belong_to

        public virtual ICollection<Category> Categories { get; set; }   //  has_and_belongs_to_many
    }

    [Table("Categories")]
    public class Category
    {
        // 主キー設定
        [Key]
        public int Id { get; set; }
        public int CategoryId { get; set; }
        // 必須
        [Required]
        public string TagName { get; set; }

        public virtual ICollection<Post> Posts { get; set; }    // has_and_belongs_to_many
    }

    public class SQLiteConfiguration : DbConfiguration
    {
        public SQLiteConfiguration()
        {
            SetProviderFactory("System.Data.SQLite", SQLiteFactory.Instance);
            SetProviderFactory("System.Data.SQLite.EF6", SQLiteProviderFactory.Instance);
            SetProviderServices("System.Data.SQLite", (DbProviderServices)SQLiteProviderFactory.Instance.GetService(typeof(DbProviderServices)));
        }
    }

    public class DBManager
    {
        private static readonly DBManager instance = new DBManager();
        private string m_dbPath;
        private string m_connStr;

        public static DBManager Instance
        {
            get
            {
                return instance;
            }
        }
        public DBManager()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            this.m_dbPath = $"{exeDir}db.sqlite3";
            this.m_connStr = $"DATA Source={this.m_dbPath}";
        }

        public void Start()
        {
            // 古いDB削除
            FileInfo file = new FileInfo(this.m_dbPath);
            file.Delete();

            // connStr = $"DATA Source=:memory:";
            using (var connection = new SQLiteConnection(this.m_connStr))
            {
                using (var context = new ApplicationDbContext(connection))
                {
                    // providerNameをコードを使って取得する。
                    // コードを使わずに、直接"System.Data.SQLite"を使ってもいい
                    // https://stackoverflow.com/questions/36060478/dbmigrator-does-not-detect-pending-migrations-after-switching-database
                    var internalContext = context.GetType().GetProperty("InternalContext", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
                    var providerName = (string)internalContext.GetType().GetProperty("ProviderName").GetValue(internalContext);

                    // Migratorが使うConfigurationを生成する。
                    // TargetDatabaseはDbMigratorの方ではなく、Configurationの方に設定しないと効果が無い。                 
                    var configuration = new Migrations.Configuration()
                    {
                        TargetDatabase = new DbConnectionInfo(context.Database.Connection.ConnectionString, providerName)
                    };

                    // DbMigratorを生成する
                    var migrator = new DbMigrator(configuration);

                    // // EF6.13では問題ないが、EF6.2の場合にUpdateのタイミングで以下の例外が吐かれないようにする対策
                    // // System.ObjectDisposedException: '破棄されたオブジェクトにアクセスできません。
                    // // オブジェクト名 'SQLiteConnection' です。'
                    // // https://stackoverflow.com/questions/47329496/updating-to-ef-6-2-0-from-ef-6-1-3-causes-cannot-access-a-disposed-object-error/47518197
                    // var _historyRepository = migrator.GetType().GetField("_historyRepository", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(migrator);
                    // var _existingConnection = _historyRepository.GetType().BaseType.GetField("_existingConnection", BindingFlags.Instance | BindingFlags.NonPublic);
                    // _existingConnection.SetValue(_historyRepository, null);

                    // Migrationを実行する。
                    migrator.Update();

                    // データベースにアクセスして保存する例
                    if (context.Blogs.Count() == 0)
                    {
                        // Blog
                        var b1 = new Blog() { Id = 1, Name = "Dummy1" };
                        var b2 = new Blog() { Id = 2, Name = "Dummy2" };
                        var b3 = new Blog() { Id = 3, Name = "Dummy3" };
                        var b4 = new Blog(4, "Dummy4"); 
                        context.Blogs.Add(b1);
                        context.Blogs.Add(b2);
                        context.Blogs.Add(b3);
                        context.Blogs.Add(b4);

                        // Category
                        
                        Category category1 = new Category { Id = 1, TagName = "C++" };
                        Category category2 = new Category { Id = 2, TagName = "Python" };
                        Category category3 = new Category { Id = 3, TagName = "Ruby" };

                        // Post
                        Post post1 = new Post() { Id = 1, Title = "PostDummy1", Blog = b1, };
                        Post post2 = new Post() { Id = 2, Title = "PostDummy2", Blog = b1, };
                        Post post3 = new Post() { Id = 3, Title = "PostDummy3", Blog = b1, };
                        Post post4 = new Post() { Id = 4, Title = "PostDummy4", Blog = b2, };
                        Post post5 = new Post() { Id = 5, Title = "PostDummy5", Blog = b3, };
                        Post post6 = new Post() { Id = 6, Title = "PostDummy6", Blog = b3, };
                        post1.Categories = new List<Category>{ category1, category3 };
                        post2.Categories = new List<Category> { category2, category3 };
                        post3.Categories = new List<Category> { category1, category2 };
                        post4.Categories = new List<Category> { category1, category2 };
                        post5.Categories = new List<Category> { category1, category2 };
                        post6.Categories = new List<Category> { category1, category2 };
                        context.Posts.Add(post1);
                        context.Posts.Add(post2);
                        context.Posts.Add(post3);
                        context.Posts.Add(post4);
                        context.Posts.Add(post5);
                        context.Posts.Add(post6);

                        /*
                        context.Posts.Add(new Post() { Id = 1, Title = "PostDummy1", Blog = b1, });
                        context.Posts.Add(new Post() { Id = 2, Title = "PostDummy2", Blog = b1, });
                        context.Posts.Add(new Post() { Id = 3, Title = "PostDummy3", Blog = b1, });
                        context.Posts.Add(new Post() { Id = 4, Title = "PostDummy4", Blog = b2, });
                        context.Posts.Add(new Post() { Id = 5, Title = "PostDummy5", Blog = b3, });
                        context.Posts.Add(new Post() { Id = 6, Title = "PostDummy6", Blog = b3, });
                        */

                        context.SaveChanges();
                    }
                }
            }
        }

        public void ReadTest()
        {
            // connStr = $"DATA Source=:memory:";
            using (var connection = new SQLiteConnection(this.m_connStr))
            {
                using (var context = new ApplicationDbContext(connection))
                {
                    // BlogIDが1以上のBlogを配列でもらう
                    var blogs = context.Blogs.Where(x => x.Id >= 1).ToArray(); // 複数
                    // BlogIDが2のBlogをもらう
                    var blog = context.Blogs.Where(x => x.Id == 2).First(); // 単数
                    // PostIDが2以下のPostを配列でもらう
                    var posts = context.Posts.Where(x => x.Id <= 2).ToArray();
                    // BologIDが1のBlogが保持しているPostを配列でもらう
                    var posts2 = context.Blogs.Where(x => x.Id == 1).SelectMany(y => y.Posts).ToArray();
                    // CategoryIDが1のCategoryを保持するPostを配列でもらう
                    var posts3 = context.Categories.Where(x => x.Id == 1).SelectMany(y => y.Posts).ToArray();
                }
            }
        }

    }
}