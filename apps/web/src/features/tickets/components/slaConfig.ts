import type { BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

export interface SlaConfig {
  text: string;
  tone: BadgeTone;
  icon: string;
}

export const SLA_CONFIGS: Record<SlaStatus, SlaConfig> = {
  WithinSla: { text: 'Within SLA', tone: 'info', icon: '⏱' },
  AtRisk: { text: 'At risk', tone: 'warning', icon: '⚠️' },
  Breached: { text: 'Breached', tone: 'danger', icon: '✕' },
  Met: { text: 'Met', tone: 'success', icon: '✓' },
  NotApplicable: { text: 'No SLA', tone: 'neutral', icon: '—' },
};
