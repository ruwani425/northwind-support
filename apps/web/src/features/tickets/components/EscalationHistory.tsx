import { useEffect, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  ticketId: number;
  refreshToken?: number;
}

export function EscalationHistory({ ticketId, refreshToken }: EscalationHistoryProps) {
  const [escalations, setEscalations] = useState<TicketEscalation[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function fetchEscalations() {
      setIsLoading(true);
      setError(null);
      try {
        const data = await ticketsApi.getEscalations(ticketId);
        if (!cancelled) {
          setEscalations(data);
        }
      } catch {
        if (!cancelled) {
          setError('Failed to load escalation history.');
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    void fetchEscalations();

    return () => {
      cancelled = true;
    };
  }, [ticketId, refreshToken]);

  if (isLoading) {
    return (
      <div className="card">
        <h2>Escalation History</h2>
        <Spinner label="Loading escalation history..." />
      </div>
    );
  }

  if (error) {
    return (
      <div className="card">
        <h2>Escalation History</h2>
        <p className="field__error">{error}</p>
      </div>
    );
  }

  if (escalations.length === 0) {
    return (
      <div className="card">
        <h2>Escalation History</h2>
        <p className="summary__note">This ticket has never been escalated.</p>
      </div>
    );
  }

  return (
    <div className="card escalation-history" data-testid="escalation-history">
      <h2>Escalation History ({escalations.length})</h2>
      <ol className="timeline">
        {escalations.map((item) => (
          <li key={item.id} className="timeline__item" data-testid="escalation-item">
            <header className="timeline__header">
              <div className="timeline__transition">
                <PriorityBadge priority={item.fromPriority} />
                <span className="timeline__arrow" aria-hidden="true">
                  →
                </span>
                <PriorityBadge priority={item.toPriority} />
              </div>
              <time className="timeline__time" dateTime={item.escalatedAtUtc}>
                {formatDateTime(item.escalatedAtUtc)}
              </time>
            </header>

            <div className="timeline__body">
              <blockquote className="timeline__reason">"{item.reason}"</blockquote>

              <dl className="timeline__meta">
                <div>
                  <dt>Escalated by</dt>
                  <dd>{item.escalatedBy}</dd>
                </div>
                <div>
                  <dt>Agent change</dt>
                  <dd>
                    {item.fromAgent?.fullName ?? 'Unassigned'} → {item.toAgent?.fullName ?? 'Unassigned'}
                  </dd>
                </div>
                {item.toDueAtUtc && (
                  <div>
                    <dt>New due date</dt>
                    <dd>{formatDateTime(item.toDueAtUtc)}</dd>
                  </div>
                )}
              </dl>
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}
