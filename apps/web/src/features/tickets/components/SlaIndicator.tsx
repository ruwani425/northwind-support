import { Badge, type BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';
import { SLA_CONFIGS } from './slaConfig';

interface SlaIndicatorProps {
  status: SlaStatus;
  showIcon?: boolean;
}

/**
 * Reusable SLA indicator displaying one of the 5 SLA states.
 * States are conveyed through explicit text, icons and tones (not by colour alone).
 */
export function SlaIndicator({ status, showIcon = true }: SlaIndicatorProps) {
  const config = SLA_CONFIGS[status] ?? {
    text: status,
    tone: 'neutral' as BadgeTone,
    icon: '',
  };

  return (
    <span className="sla-indicator" data-testid="sla-indicator" data-sla-status={status}>
      <Badge tone={config.tone}>
        {showIcon && (
          <span className="sla-indicator__icon" aria-hidden="true">
            {config.icon}{' '}
          </span>
        )}
        <span className="sla-indicator__text">{config.text}</span>
      </Badge>
    </span>
  );
}
