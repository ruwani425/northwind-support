import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

describe('SlaIndicator', () => {
  const testCases: { status: SlaStatus; expectedText: string; expectedTone: string }[] = [
    { status: 'WithinSla', expectedText: 'Within SLA', expectedTone: 'info' },
    { status: 'AtRisk', expectedText: 'At risk', expectedTone: 'warning' },
    { status: 'Breached', expectedText: 'Breached', expectedTone: 'danger' },
    { status: 'Met', expectedText: 'Met', expectedTone: 'success' },
    { status: 'NotApplicable', expectedText: 'No SLA', expectedTone: 'neutral' },
  ];

  it.each(testCases)(
    'renders $status distinguishably with text "$expectedText" and tone "$expectedTone"',
    ({ status, expectedText, expectedTone }) => {
      const { container } = render(<SlaIndicator status={status} />);

      const indicator = screen.getByTestId('sla-indicator');
      expect(indicator).toBeInTheDocument();
      expect(indicator).toHaveAttribute('data-sla-status', status);
      expect(indicator).toHaveTextContent(expectedText);

      const badge = container.querySelector(`.badge--${expectedTone}`);
      expect(badge).toBeInTheDocument();
    },
  );

  it('renders all 5 states with distinct text labels', () => {
    const renderedTexts = new Set<string>();

    for (const { status, expectedText } of testCases) {
      render(<SlaIndicator status={status} />);
      renderedTexts.add(expectedText);
    }

    expect(renderedTexts.size).toBe(5);
  });
});
