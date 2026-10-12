import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';

export type CodeBlockProps = {
  /** Optional caption shown above the block. */
  readonly label?: string;
  /** The monospace text to display (a URL, token, or JSON document). */
  readonly value: string;
  /** Marks the content as sensitive so it renders with a subtle warning tint. */
  readonly sensitive?: boolean;
};

/**
 * Stateless monospace block for displaying a URL, token, or JSON document. Wraps long values and preserves
 * whitespace so the artifact is legible without truncation. Prop-driven so it renders in isolation.
 */
export function CodeBlock({ label, value, sensitive = false }: CodeBlockProps) {
  return (
    <Box>
      {label ? (
        <Typography variant="caption" color="text.secondary" component="div" gutterBottom>
          {label}
        </Typography>
      ) : null}
      <Box
        component="pre"
        data-testid="code-block"
        sx={{
          m: 0,
          p: 1.5,
          borderRadius: 1,
          fontSize: '0.8125rem',
          fontFamily: 'monospace',
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-all',
          overflowX: 'auto',
          bgcolor: sensitive ? 'warning.50' : 'action.hover',
          border: '1px solid',
          borderColor: sensitive ? 'warning.light' : 'divider',
        }}
      >
        {value}
      </Box>
    </Box>
  );
}
