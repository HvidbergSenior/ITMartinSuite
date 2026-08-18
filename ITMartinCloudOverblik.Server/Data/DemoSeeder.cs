using ITMartinCloudOverblik.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinCloudOverblik.Server.Data;

// Minimal demo-tier seed - a couple of example audit leads (clearly fake
// names) so a visitor sees the admin overview populated. Only runs when
// CloudOverblik:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(CloudDbContext db)
    {
        if (await db.Leads.AnyAsync())
            return;

        db.Leads.AddRange(
            new AuditLead
            {
                Name = "Demo Testperson",
                Email = "demo@example.com",
                Phone = "12345678",
                FamilySize = 4,
                ServicesJson = "[\"Netflix\",\"Spotify\",\"iCloud\",\"Disney+\"]",
                MonthlyCost = 389m,
                MonthlySaving = 140m,
                Notes = "Eksempel-lead til demoformål.",
            },
            new AuditLead
            {
                Name = "Anden Demoperson",
                Email = "demo2@example.com",
                Phone = "87654321",
                FamilySize = 2,
                ServicesJson = "[\"Netflix\",\"HBO Max\",\"Google One\"]",
                MonthlyCost = 219m,
                MonthlySaving = 60m,
                Contacted = true,
            });

        await db.SaveChangesAsync();
    }
}
