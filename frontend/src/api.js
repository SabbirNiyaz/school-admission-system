const BASE = import.meta.env.VITE_API_URL || 'http://localhost:8080';

const MESSAGES = {
  INVALID_STUDENT_ID: 'জন্ম নিবন্ধন নম্বর অবশ্যই ১৭ অঙ্কের হতে হবে।',
  SEAT_FULL: 'দুঃখিত, এই ক্লাসে আর কোনো আসন খালি নেই।',
  ALREADY_APPLIED: 'আপনার এই ক্লাসে ইতিমধ্যে একটি সক্রিয় আবেদন বা ভর্তি আছে।',
  CLASS_NOT_FOUND: 'এই ক্লাসটি পাওয়া যায়নি।',
  HOLD_NOT_FOUND: 'এই আবেদনটি পাওয়া যায়নি।',
  HOLD_EXPIRED: 'সময়সীমা শেষ হয়ে গেছে, তাই আবেদনটি আর নিশ্চিত করা যাবে না।',
  HOLD_NOT_ACTIVE: 'এই আবেদনটি আর নিশ্চিত করা যাবে না।',
  HOLD_NOT_CANCELLABLE: 'এই আবেদনটি আর বাতিল করা যাবে না।',
};

export async function api(path, { method = 'GET', body, studentId } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (studentId) headers['X-Student-Id'] = studentId;

  let response;
  try {
    response = await fetch(`${BASE}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new Error('সার্ভারের সাথে যোগাযোগ করা যাচ্ছে না। একটু পরে আবার চেষ্টা করুন।');
  }

  if (response.status === 204) return null;
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(MESSAGES[data?.code] ?? data?.message ?? 'কিছু একটা ভুল হয়েছে, আবার চেষ্টা করুন।');
  }
  return data;
}
