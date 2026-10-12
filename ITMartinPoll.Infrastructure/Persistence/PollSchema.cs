using ITMartinPoll.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPoll.Infrastructure.Persistence;

// poll.db was created with EnsureCreated (no migrations); tables and columns added later are bolted on here at start.
// Every statement is idempotent - an "already exists" just means it ran before. Moved from the Server's Program.cs 2026-10-12.
public static class PollSchema
{
    public static void Upgrade(PollDb db)
    {
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Sessions" (
            "Id"        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Title"     TEXT    NOT NULL,
            "IsActive"  INTEGER NOT NULL,
            "CreatedAt" TEXT    NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "SessionImages" (
            "Id"        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "SessionId" INTEGER NOT NULL,
            "FileName"  TEXT    NOT NULL,
            "SortOrder" INTEGER NOT NULL,
            FOREIGN KEY ("SessionId") REFERENCES "Sessions" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS "ImageRatings" (
            "Id"      INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "ImageId" INTEGER NOT NULL,
            "Score"   INTEGER NOT NULL,
            "Comment" TEXT    NOT NULL,
            "RatedAt" TEXT    NOT NULL,
            FOREIGN KEY ("ImageId") REFERENCES "SessionImages" ("Id") ON DELETE CASCADE
        );
    """);
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"Deadline\" TEXT;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"CoverImageName\" TEXT;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"Description\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"ImageRatings\" ADD COLUMN \"VoterToken\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"ImageRatings\" ADD COLUMN \"VoterName\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"Question\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Votes\" ADD COLUMN \"VoterName\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Packages" (
            "Id"        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Title"     TEXT    NOT NULL DEFAULT '',
            "Intro"     TEXT    NOT NULL DEFAULT '',
            "IsActive"  INTEGER NOT NULL DEFAULT 1,
            "CreatedAt" TEXT    NOT NULL
        );
    """);
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Packages\" ADD COLUMN \"Instructions\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Packages\" ADD COLUMN \"Audience\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Polls\" ADD COLUMN \"AllowMultiple\" INTEGER NOT NULL DEFAULT 0;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Polls\" ADD COLUMN \"RequireContact\" INTEGER NOT NULL DEFAULT 0;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Votes\" ADD COLUMN \"Email\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Votes\" ADD COLUMN \"WantsAuctionNews\" INTEGER NOT NULL DEFAULT 0;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Polls\" ADD COLUMN \"PackageId\" INTEGER NULL;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"PackageId\" INTEGER NULL;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"SessionImages\" ADD COLUMN \"Caption\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"SessionImages\" ADD COLUMN \"Description\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"DatePollResponses\" ADD COLUMN \"IsPreferred\" INTEGER NOT NULL DEFAULT 0;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"DatePolls\" ADD COLUMN \"Password\" TEXT;"); }
    catch { /* column already exists */ }
    // Help sign-up ("Giv en hånd", /tilmeld/{id}).
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "HelpEvents" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, "Title" TEXT NOT NULL DEFAULT '', "Body" TEXT NOT NULL DEFAULT '',
            "ImageName" TEXT NULL, "CreatedAt" TEXT NOT NULL, "Deadline" TEXT NULL, "IsActive" INTEGER NOT NULL DEFAULT 1);
    """);
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "HelpSlots" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, "HelpEventId" INTEGER NOT NULL, "Label" TEXT NOT NULL DEFAULT '',
            "SortOrder" INTEGER NOT NULL DEFAULT 0);
    """);
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "HelpTasks" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, "HelpEventId" INTEGER NOT NULL, "Label" TEXT NOT NULL DEFAULT '',
            "SortOrder" INTEGER NOT NULL DEFAULT 0, "AddedBy" TEXT NOT NULL DEFAULT '');
    """);
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "HelpSignups" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, "HelpEventId" INTEGER NOT NULL, "Name" TEXT NOT NULL DEFAULT '',
            "Phone" TEXT NOT NULL DEFAULT '', "Comment" TEXT NOT NULL DEFAULT '', "SlotIds" TEXT NOT NULL DEFAULT '',
            "TaskIds" TEXT NOT NULL DEFAULT '', "SignedUpAt" TEXT NOT NULL);
    """);
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"HelpEvents\" ADD COLUMN \"Photos\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Customers" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, "Name" TEXT NOT NULL DEFAULT '', "Email" TEXT NOT NULL DEFAULT '',
            "Phone" TEXT NOT NULL DEFAULT '', "Source" TEXT NOT NULL DEFAULT '', "ConsentText" TEXT NOT NULL DEFAULT '',
            "ConsentAt" TEXT NOT NULL, "WantsAuctionNews" INTEGER NOT NULL DEFAULT 1, "UnsubscribeToken" TEXT NOT NULL DEFAULT '',
            "UnsubscribedAt" TEXT NULL, "Note" TEXT NOT NULL DEFAULT '', "CreatedAt" TEXT NOT NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Customers_Email" ON "Customers" ("Email");
    """);
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Customers\" ADD COLUMN \"Interests\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "HelpChat" (
            "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, "HelpEventId" INTEGER NOT NULL, "Name" TEXT NOT NULL DEFAULT '',
            "Text" TEXT NOT NULL DEFAULT '', "IsOrganiser" INTEGER NOT NULL DEFAULT 0, "SentAt" TEXT NOT NULL);
    """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "DatePolls" (
            "Id"          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Title"       TEXT    NOT NULL,
            "Description" TEXT    NOT NULL DEFAULT '',
            "ImageName"   TEXT,
            "CreatedAt"   TEXT    NOT NULL,
            "Deadline"    TEXT,
            "IsActive"    INTEGER NOT NULL DEFAULT 1,
            "Password"    TEXT
        );
        CREATE TABLE IF NOT EXISTS "DatePollDates" (
            "Id"         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DatePollId" INTEGER NOT NULL,
            "Date"       TEXT    NOT NULL,
            "SortOrder"  INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY ("DatePollId") REFERENCES "DatePolls" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS "DatePollResponses" (
            "Id"          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DateId"      INTEGER NOT NULL,
            "VoterName"   TEXT    NOT NULL DEFAULT '',
            "Status"      TEXT    NOT NULL DEFAULT 'Maybe',
            "Comment"     TEXT    NOT NULL DEFAULT '',
            "RespondedAt" TEXT    NOT NULL,
            "IsPreferred" INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY ("DateId") REFERENCES "DatePollDates" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS "DatePollChat" (
            "Id"         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DatePollId" INTEGER NOT NULL,
            "SenderName" TEXT    NOT NULL DEFAULT '',
            "Text"       TEXT    NOT NULL DEFAULT '',
            "SentAt"     TEXT    NOT NULL,
            FOREIGN KEY ("DatePollId") REFERENCES "DatePolls" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS "DatePollHosts" (
            "Id"         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DatePollId" INTEGER NOT NULL,
            "VoterName"  TEXT    NOT NULL DEFAULT '',
            "Place"      TEXT    NOT NULL DEFAULT '',
            "OfferedAt"  TEXT    NOT NULL,
            FOREIGN KEY ("DatePollId") REFERENCES "DatePolls" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_DatePollHosts_Poll_Voter" ON "DatePollHosts" ("DatePollId", "VoterName");
        CREATE TABLE IF NOT EXISTS "DatePollImages" (
            "Id"         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DatePollId" INTEGER NOT NULL,
            "FileName"   TEXT    NOT NULL DEFAULT '',
            "SortOrder"  INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY ("DatePollId") REFERENCES "DatePolls" ("Id") ON DELETE CASCADE
        );
    """);

    }
}
