/**
 * Renders console arguments the way the console prints them, closely enough to identify the message in a failure:
 * a leading format string has its `%s`/`%d`/`%i`/`%f`/`%o`/`%O`/`%j`/`%c` specifiers substituted in order (`%c` styling
 * is dropped, `%%` is a literal percent), and remaining arguments are appended space-separated. It is dependency-free
 * so that every workspace — including the browser-typed ones without Node's `util` — type-checks the setup that uses it.
 */
export function formatConsoleArgs(args: readonly unknown[]): string {
  const [first, ...rest] = args;
  if (typeof first !== 'string') {
    return args.map(describe).join(' ');
  }
  let next = 0;
  const message = first.replace(/%([sdifoOjc%])/g, (token, specifier: string) => {
    if (specifier === '%') {
      return '%';
    }
    if (next >= rest.length) {
      return token;
    }
    const value = rest[next++];
    switch (specifier) {
      case 'c':
        return '';
      case 'd':
      case 'i':
        return String(typeof value === 'bigint' ? value : Math.trunc(Number(value)));
      case 'f':
        return String(Number(value));
      default:
        return describe(value);
    }
  });
  return [message, ...rest.slice(next).map(describe)].join(' ');
}

function describe(value: unknown): string {
  if (typeof value === 'string') {
    return value;
  }
  if (value instanceof Error) {
    return value.stack ?? `${value.name}: ${value.message}`;
  }
  if (typeof value === 'object' && value !== null) {
    try {
      return JSON.stringify(value);
    } catch {
      return Object.prototype.toString.call(value);
    }
  }
  return String(value);
}
