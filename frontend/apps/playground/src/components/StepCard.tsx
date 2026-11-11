import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

/** The lifecycle state of a single step in the flow walkthrough. */
export type StepStatus = 'idle' | 'active' | 'done' | 'error';

export type StepCardProps = {
  /** The 1-based step number shown in the leading badge. */
  readonly index: number;
  readonly title: string;
  readonly status: StepStatus;
  readonly description?: string;
  readonly children?: ReactNode;
};

const STATUS_LABEL: Record<StepStatus, string> = {
  idle: 'Pending',
  active: 'Ready',
  done: 'Done',
  error: 'Error',
};

const STATUS_COLOR: Record<StepStatus, 'default' | 'primary' | 'success' | 'error'> = {
  idle: 'default',
  active: 'primary',
  done: 'success',
  error: 'error',
};

/**
 * Stateless card for one step of the authorization-code + PKCE walkthrough: a numbered header, a status chip, an
 * optional description, and arbitrary content (inputs, artifacts, actions) supplied by the container. Prop-driven so
 * it renders in isolation.
 */
export function StepCard({ index, title, status, description, children }: StepCardProps) {
  return (
    <Card variant="outlined" data-testid={`step-${index}`} data-status={status}>
      <CardContent>
        <Stack
          direction="row"
          spacing={1.5}
          sx={{ alignItems: 'center', mb: description ? 0.5 : 1 }}
        >
          <Chip size="small" label={index} color={STATUS_COLOR[status]} />
          <Typography variant="h6" component="h2" sx={{ flexGrow: 1 }}>
            {title}
          </Typography>
          <Chip
            size="small"
            variant="outlined"
            color={STATUS_COLOR[status]}
            label={STATUS_LABEL[status]}
          />
        </Stack>
        {description ? (
          <Typography sx={{ color: 'text.secondary', mb: 1.5 }}>{description}</Typography>
        ) : null}
        {children ? <Box>{children}</Box> : null}
      </CardContent>
    </Card>
  );
}
