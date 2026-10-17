import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import type { TicketEscalationResult } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

describe('EscalationPanel', () => {
  const onEscalated = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('blocks submission with short reason (< 5 characters) and does not call the API', async () => {
    const ticket = ticketDetail({ priority: 'Medium', status: 'InProgress' });

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    // Open escalation form
    fireEvent.click(screen.getByTestId('open-escalate-btn'));

    const reasonInput = screen.getByLabelText(/reason for escalation/i);
    const submitBtn = screen.getByTestId('submit-escalate-btn');

    // Type short reason: 3 characters
    fireEvent.change(reasonInput, { target: { value: 'bad' } });
    fireEvent.click(submitBtn);

    expect(await screen.findByText(/reason must be at least 5 characters/i)).toBeInTheDocument();
    expect(ticketsApi.escalate).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('shows 409 error message from server and leaves ticket unchanged', async () => {
    const ticket = ticketDetail({ priority: 'High', status: 'InProgress' });
    const conflictMessage = 'Ticket TCK-0012 is already at Critical priority and cannot be escalated.';

    vi.mocked(ticketsApi.escalate).mockRejectedValueOnce(
      new ApiError(409, conflictMessage, {
        status: 409,
        title: 'Conflict',
        detail: conflictMessage,
      }),
    );

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    // Open form
    fireEvent.click(screen.getByTestId('open-escalate-btn'));

    const reasonInput = screen.getByLabelText(/reason for escalation/i);
    fireEvent.change(reasonInput, { target: { value: 'Customer issue severity increased.' } });

    const submitBtn = screen.getByTestId('submit-escalate-btn');
    fireEvent.click(submitBtn);

    expect(await screen.findByTestId('escalation-server-error')).toHaveTextContent(conflictMessage);
    expect(ticketsApi.escalate).toHaveBeenCalledTimes(1);
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('successfully escalates with valid reason and notifies parent with updated ticket', async () => {
    const ticket = ticketDetail({ priority: 'Low', status: 'InProgress' });
    const updatedTicket = { ...ticket, priority: 'Medium' as const };
    const mockResult: TicketEscalationResult = {
      ticket: updatedTicket,
      escalation: {
        id: 1,
        ticketId: ticket.id,
        fromPriority: 'Low',
        toPriority: 'Medium',
        fromAgent: null,
        toAgent: null,
        fromDueAtUtc: null,
        toDueAtUtc: null,
        reason: 'Customer requested expedited resolution.',
        escalatedBy: 'Team Lead',
        escalatedAtUtc: '2026-10-18T10:00:00Z',
      },
    };

    vi.mocked(ticketsApi.escalate).mockResolvedValueOnce(mockResult);

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    fireEvent.click(screen.getByTestId('open-escalate-btn'));

    const reasonInput = screen.getByLabelText(/reason for escalation/i);
    fireEvent.change(reasonInput, { target: { value: 'Customer requested expedited resolution.' } });

    fireEvent.click(screen.getByTestId('submit-escalate-btn'));

    await waitFor(() => {
      expect(ticketsApi.escalate).toHaveBeenCalledWith(ticket.id, {
        reason: 'Customer requested expedited resolution.',
        escalatedBy: 'Team Lead',
      });
      expect(onEscalated).toHaveBeenCalledWith(updatedTicket);
    });
  });

  it('disables or explains when ticket is Critical', () => {
    const ticket = ticketDetail({ priority: 'Critical', status: 'InProgress' });

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    expect(screen.getByRole('note')).toHaveTextContent(/already at Critical priority and cannot be escalated/i);
    expect(screen.queryByTestId('open-escalate-btn')).not.toBeInTheDocument();
  });

  it('disables or explains when ticket is Closed or Resolved', () => {
    const ticket = ticketDetail({ priority: 'Medium', status: 'Closed' });

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    expect(screen.getByRole('note')).toHaveTextContent(/is Closed and cannot be escalated/i);
    expect(screen.queryByTestId('open-escalate-btn')).not.toBeInTheDocument();
  });
});
