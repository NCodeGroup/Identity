import type { Meta, StoryObj } from '@storybook/react';
import { SignInScreen } from './SignInScreen.js';

const meta: Meta<typeof SignInScreen> = {
  title: 'Auth/SignInScreen',
  component: SignInScreen,
  args: { onSignIn: () => undefined, productName: 'Management' },
};
export default meta;

type Story = StoryObj<typeof SignInScreen>;

export const Default: Story = {};
export const Busy: Story = { args: { isBusy: true } };
export const WithError: Story = { args: { error: 'Authorization was denied.' } };
