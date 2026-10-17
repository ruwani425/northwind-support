import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearStoredAuth, getStoredToken, request, setStoredToken } from './client';

describe('client api request', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    clearStoredAuth();
  });

  it('automatically attaches Authorization Bearer header when token is stored', async () => {
    setStoredToken('test-secret-token');
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ success: true }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await request('/tickets');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [, init] = fetchMock.mock.calls[0];
    const headers = init.headers as Headers;
    expect(headers.get('Authorization')).toBe('Bearer test-secret-token');
  });

  it('clears stored session and dispatches auth:unauthorized on 401 response', async () => {
    setStoredToken('invalid-token');
    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
      json: () => Promise.resolve({ title: 'Unauthorized', detail: 'Invalid credentials.' }),
    });
    vi.stubGlobal('fetch', fetchMock);

    const unauthorizedListener = vi.fn();
    window.addEventListener('auth:unauthorized', unauthorizedListener);

    await expect(request('/tickets')).rejects.toThrow();

    expect(getStoredToken()).toBeNull();
    expect(unauthorizedListener).toHaveBeenCalledTimes(1);

    window.removeEventListener('auth:unauthorized', unauthorizedListener);
  });
});
