import type { Preview } from '@storybook/react';
import { AppThemeProvider } from '../src/theme/AppThemeProvider.js';

const preview: Preview = {
  parameters: {
    controls: { matchers: { color: /(background|color)$/i, date: /Date$/i } },
  },
  decorators: [
    (Story) => (
      <AppThemeProvider>
        <Story />
      </AppThemeProvider>
    ),
  ],
};

export default preview;
