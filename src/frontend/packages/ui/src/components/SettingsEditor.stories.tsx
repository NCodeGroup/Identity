import type { Meta, StoryObj } from '@storybook/react-vite';
import { SettingsEditor } from './SettingsEditor.js';

const meta: Meta<typeof SettingsEditor> = {
  title: 'Management/SettingsEditor',
  component: SettingsEditor,
};
export default meta;

type Story = StoryObj<typeof SettingsEditor>;

export const Populated: Story = {
  args: {
    initialJson: JSON.stringify({ accessTokenLifetime: 3600 }, null, 2),
    effectiveJson: JSON.stringify(
      { accessTokenLifetime: 3600, idTokenLifetime: 300, issuer: 'https://op.test' },
      null,
      2,
    ),
    onSave: () => undefined,
  },
};

export const Empty: Story = {
  args: {
    initialJson: '{}',
    effectiveJson: JSON.stringify({ issuer: 'https://op.test' }, null, 2),
    onSave: () => undefined,
  },
};

export const WithError: Story = {
  args: {
    initialJson: JSON.stringify({ accessTokenLifetime: 3600 }, null, 2),
    effectiveJson: JSON.stringify({ accessTokenLifetime: 3600 }, null, 2),
    error: 'The settings could not be saved because they were modified by someone else.',
    onSave: () => undefined,
  },
};
