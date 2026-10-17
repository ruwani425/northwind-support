import { useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (updatedTicket: TicketDetail) => void;
}

export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('Team Lead');
  const [error, setError] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isOpen, setIsOpen] = useState(false);

  const isCritical = ticket.priority === 'Critical';
  const isClosedOrResolved = ticket.status === 'Resolved' || ticket.status === 'Closed';
  const canEscalate = !isCritical && !isClosedOrResolved;

  const validateReason = (value: string): string | null => {
    const trimmed = value.trim();
    if (!trimmed) {
      return 'Reason is required.';
    }
    if (trimmed.length < 5) {
      return 'Reason must be at least 5 characters.';
    }
    if (trimmed.length > 500) {
      return 'Reason must be 500 characters or fewer.';
    }
    return null;
  };

  const handleReasonChange = (value: string) => {
    setReason(value);
    if (validationError) {
      setValidationError(validateReason(value));
    }
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);

    const reasonError = validateReason(reason);
    if (reasonError) {
      setValidationError(reasonError);
      return;
    }

    if (!escalatedBy.trim()) {
      setError('EscalatedBy is required.');
      return;
    }

    setIsSubmitting(true);
    try {
      const result = await ticketsApi.escalate(ticket.id, {
        reason: reason.trim(),
        escalatedBy: escalatedBy.trim(),
      });
      setReason('');
      setValidationError(null);
      setError(null);
      setIsOpen(false);
      onEscalated(result.ticket);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || (err.status === 409 ? 'Ticket cannot be escalated.' : 'Failed to escalate ticket.'));
      } else {
        setError('An unexpected error occurred while escalating the ticket.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="card escalation-panel" data-testid="escalation-panel">
      <h2>Ticket Escalation</h2>

      {!canEscalate ? (
        <div className="escalation-panel__notice" role="note">
          {isCritical ? (
            <p>
              This ticket is already at <strong>Critical</strong> priority and cannot be escalated further.
            </p>
          ) : (
            <p>
              This ticket is <strong>{ticket.status}</strong> and cannot be escalated.
            </p>
          )}
        </div>
      ) : !isOpen ? (
        <div>
          <p className="summary__note">
            Escalating will raise priority from <strong>{ticket.priority}</strong> to the next level, recalculate
            the SLA due date, and re-evaluate agent assignment.
          </p>
          <button
            type="button"
            className="button button--warning"
            onClick={() => setIsOpen(true)}
            data-testid="open-escalate-btn"
          >
            Escalate ticket
          </button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} noValidate className="form">
          {error && (
            <div className="error" role="alert" data-testid="escalation-server-error">
              <p>{error}</p>
            </div>
          )}

          <div className="field">
            <label className="field__label" htmlFor="escalate-reason">
              Reason for escalation <span className="field__required">*</span>
            </label>
            <textarea
              id="escalate-reason"
              rows={3}
              placeholder="Explain why this ticket is being escalated (5–500 characters)..."
              value={reason}
              onChange={(e) => handleReasonChange(e.target.value)}
              disabled={isSubmitting}
              aria-invalid={Boolean(validationError)}
              aria-describedby={validationError ? 'escalate-reason-error' : undefined}
            />
            <div className="field__meta">
              {validationError ? (
                <span id="escalate-reason-error" className="field__error" role="alert">
                  {validationError}
                </span>
              ) : (
                <span className="field__hint">5 to 500 characters</span>
              )}
              <span className="field__count">{reason.trim().length}/500</span>
            </div>
          </div>

          <div className="field">
            <label className="field__label" htmlFor="escalate-by">
              Escalated by
            </label>
            <input
              id="escalate-by"
              type="text"
              value={escalatedBy}
              onChange={(e) => setEscalatedBy(e.target.value)}
              disabled={isSubmitting}
              placeholder="Your name or email"
            />
          </div>

          <div className="button-row">
            <button
              type="submit"
              className="button button--warning"
              disabled={isSubmitting}
              data-testid="submit-escalate-btn"
            >
              {isSubmitting ? 'Escalating...' : 'Confirm escalation'}
            </button>
            <button
              type="button"
              className="button button--ghost"
              disabled={isSubmitting}
              onClick={() => {
                setIsOpen(false);
                setError(null);
                setValidationError(null);
              }}
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
