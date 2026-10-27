using BirthdayBot.Storage;
using BirthdayBot.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Scheduling;

public sealed class BirthdayScheduler(BirthdayStore birthdays, AnnouncementLedger ledger, Announcer announcer, TimeProvider time, ILogger<BirthdayScheduler> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        try {
            await TickAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken)) await TickAsync(stoppingToken);
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task TickAsync(CancellationToken ct) {
        try {
            await AnnounceDueAsync(ct);
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            logger.LogError(ex, "Birthday scheduler tick failed");
        }
    }

    private async Task AnnounceDueAsync(CancellationToken ct) {
        var utcNow = time.GetUtcNow();
        var rows = await birthdays.ListCandidatesAsync(DueBirthdays.CandidateMonthDays(utcNow), ct);
        var due = DueBirthdays.Select(rows, utcNow, TimeZoneCatalog.Resolve);
        if (due.Count == 0) return;
        var posted = await ledger.AlreadyPostedAsync(due.Select(Key).ToList(), ct);
        var pending = due.Where(d => !posted.Contains(Key(d)));
        foreach (var guild in pending.GroupBy(d => d.GuildId)) {
            var members = guild.ToList();
            if (!await announcer.PostAsync(guild.Key, members, utcNow, ct)) continue;
            await ledger.RecordAsync(members.Select(Key).ToList(), utcNow, ct);
        }
    }

    private static AnnouncementKey Key(DueBirthday due) => new(due.GuildId, due.UserId, due.LocalDate);
}
