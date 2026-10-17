using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations", t =>
        {
            t.HasCheckConstraint("CK_TicketEscalations_FromPriority", "[FromPriority] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_TicketEscalations_ToPriority", "[ToPriority] BETWEEN 1 AND 4");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromPriority).HasConversion<int>().IsRequired();
        builder.Property(e => e.ToPriority).HasConversion<int>().IsRequired();

        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.EscalatedBy).HasMaxLength(100).IsRequired();

        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)").IsRequired();

        // An escalation is part of a ticket's audit trail: deleting a ticket cascades its history.
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Historical agent references are kept even if an agent record is modified.
        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.FromAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.ToAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index on TicketId supports fast lookup for GET /api/tickets/{id}/escalations,
        // and including EscalatedAtUtc gives chronological ordering directly from the index.
        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc })
            .HasDatabaseName("IX_TicketEscalations_TicketId_EscalatedAtUtc");
    }
}
