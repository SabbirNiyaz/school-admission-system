import { useEffect, useState } from 'react';
import { api } from './api.js';
import { formatCountdown, STATUS_LABEL } from './utils.js';

export function ClassCreate() {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [message, setMessage] = useState(null);

  async function submit(event) {
    event.preventDefault();
    setMessage(null);
    try {
      const created = await api('/classes', {
        method: 'POST',
        body: { name, totalSeats: Number(seats) },
      });
      setMessage({ ok: true, text: `"${created.name}" তৈরি হয়েছে (${created.totalSeats} টি আসন)।` });
      setName('');
      setSeats('');
    } catch (error) {
      setMessage({ ok: false, text: error.message });
    }
  }

  return (
    <section className="card form-card">
      <div className="section-heading">
        <div><p className="eyebrow">ADMINISTRATION</p><h2>নতুন ক্লাস তৈরি</h2></div>
      </div>
      <p className="hint">প্রতিটি শিফট আলাদা ক্লাস হিসেবে তৈরি করুন, যেমন “Class 6 - Morning Shift”।</p>
      <form onSubmit={submit} className="form">
        <label>ক্লাসের নাম<input required maxLength="200" value={name} onChange={(e) => setName(e.target.value)} placeholder="যেমন Class 6 - Morning Shift" /></label>
        <label>আসন সংখ্যা<input required type="number" min="1" max="100000" value={seats} onChange={(e) => setSeats(e.target.value)} placeholder="আসন সংখ্যা" /></label>
        <button type="submit">ক্লাস তৈরি করুন</button>
      </form>
      {message && <p className={message.ok ? 'success' : 'error'}>{message.text}</p>}
    </section>
  );
}

export function ClassList({ studentId, onApplied }) {
  const [classes, setClasses] = useState([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState('');

  useEffect(() => {
    let alive = true;
    const load = async () => {
      try {
        const data = await api('/classes');
        if (alive) { setClasses(data); setError(''); }
      } catch (e) {
        if (alive) setError(e.message);
      }
    };
    load();
    const timer = setInterval(load, 3000);
    return () => { alive = false; clearInterval(timer); };
  }, []);

  async function apply(cls) {
    if (!studentId) {
      setError('আবেদন করতে আগে উপরে ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন।');
      return;
    }
    setBusy(cls.id);
    setError('');
    try {
      const hold = await api(`/classes/${cls.id}/holds`, { method: 'POST', studentId });
      onApplied(hold.holdId);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy('');
    }
  }

  return (
    <section>
      <div className="section-heading">
        <div><p className="eyebrow">AVAILABLE CLASSES</p><h2>ক্লাস তালিকা</h2></div>
        <span className="live-indicator"><i /> লাইভ আপডেট</span>
      </div>
      {error && <p className="error alert">{error}</p>}
      <div className="grid">
        {classes.map((cls) => (
          <article className="card class-card" key={cls.id}>
            <div className="class-card-top"><span className="class-icon">▤</span><span className="seat-tag">{cls.availableSeats} আসন খালি</span></div>
            <h3>{cls.name}</h3>
            <div className="stats">
              <div><span>মোট আসন</span><strong>{cls.totalSeats}</strong></div>
              <div><span>সংরক্ষিত</span><strong>{cls.heldSeats}</strong></div>
              <div><span>নিশ্চিত ভর্তি</span><strong>{cls.confirmedSeats}</strong></div>
            </div>
            <button className="full-button" disabled={cls.availableSeats === 0 || busy === cls.id} onClick={() => apply(cls)}>
              {busy === cls.id ? 'অপেক্ষা করুন...' : cls.availableSeats === 0 ? 'আসন পূর্ণ' : 'আবেদন করুন'}
            </button>
          </article>
        ))}
      </div>
      {classes.length === 0 && !error && <div className="empty card">এখনো কোনো ক্লাস তৈরি হয়নি। অ্যাডমিন প্যানেল থেকে প্রথম ক্লাসটি তৈরি করুন।</div>}
    </section>
  );
}

export function HoldDetail({ holdId, studentId, onBack }) {
  const [hold, setHold] = useState(null);
  const [className, setClassName] = useState('');
  const [error, setError] = useState('');
  const [now, setNow] = useState(Date.now());

  useEffect(() => {
    let alive = true;
    const load = async () => {
      try {
        const data = await api(`/holds/${holdId}`);
        if (alive) { setHold(data); setError(''); }
      } catch (e) {
        if (alive) setError(e.message);
      }
    };
    load();
    const sync = setInterval(load, 3000);
    const tick = setInterval(() => setNow(Date.now()), 1000);
    return () => { alive = false; clearInterval(sync); clearInterval(tick); };
  }, [holdId]);

  useEffect(() => {
    if (!hold) return;
    api(`/classes/${hold.classId}`).then((cls) => setClassName(cls.name)).catch(() => {});
  }, [hold?.classId]);

  if (!hold) {
    return <section className="card"><p>{error ? <span className="error">{error}</span> : 'লোড হচ্ছে...'}</p><button onClick={onBack}>ফিরে যান</button></section>;
  }

  const remaining = new Date(hold.expiresAt).getTime() - now;
  const status = hold.status === 'HELD' && remaining <= 0 ? 'EXPIRED' : hold.status;

  async function confirm() {
    setError('');
    try {
      const result = await api(`/holds/${holdId}/confirm`, { method: 'POST', studentId });
      setHold((current) => ({ ...current, status: result.status }));
    } catch (e) { setError(e.message); }
  }

  async function cancel() {
    setError('');
    try {
      await api(`/holds/${holdId}`, { method: 'DELETE', studentId });
      onBack();
    } catch (e) { setError(e.message); }
  }

  return (
    <section className="card hold-card">
      <p className="eyebrow">APPLICATION DETAILS</p>
      <h2>আমার আবেদন</h2>
      {className && <p>ক্লাস: <strong>{className}</strong></p>}
      <p className="hint">আবেদন নম্বর: <code>{holdId}</code></p>
      <div className="status-panel">
        <span>আবেদনের অবস্থা</span>
        <strong className={`badge ${status}`}>{STATUS_LABEL[status]}</strong>
      </div>
      {status === 'HELD' && <div className="countdown"><span>ভর্তি ফি জমার সময় বাকি</span><strong>{formatCountdown(remaining)}</strong></div>}
      {status === 'EXPIRED' && <p className="error alert">সময়সীমা শেষ হয়েছে। আসনটি এখন অন্যদের জন্য খোলা।</p>}
      {error && <p className="error alert">{error}</p>}
      <div className="actions">
        <button disabled={status !== 'HELD'} onClick={confirm}>ভর্তি ফি দিন</button>
        <button className="danger" disabled={status !== 'HELD'} onClick={cancel}>আবেদন বাতিল</button>
        <button className="secondary" onClick={onBack}>ফিরে যান</button>
      </div>
      {status === 'HELD' && <p className="hint">“ভর্তি ফি দিন” চাপলে ধরে নেওয়া হবে পেমেন্ট সফল হয়েছে। এখানে কোনো বাস্তব পেমেন্ট গেটওয়ে যুক্ত নেই।</p>}
    </section>
  );
}

export function MyHolds({ studentId, onOpen }) {
  const [items, setItems] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!studentId) { setItems([]); return; }
    let alive = true;
    const load = async () => {
      try {
        const data = await api('/me/holds', { studentId });
        if (alive) { setItems(data); setError(''); }
      } catch (e) {
        if (alive) setError(e.message);
      }
    };
    load();
    const timer = setInterval(load, 3000);
    return () => { alive = false; clearInterval(timer); };
  }, [studentId]);

  if (!studentId) return <p className="hint">আপনার আবেদনগুলো দেখতে উপরে ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন।</p>;

  return (
    <section>
      <div className="section-heading"><div><p className="eyebrow">YOUR APPLICATIONS</p><h2>আমার সব আবেদন</h2></div></div>
      {error && <p className="error alert">{error}</p>}
      {items.length === 0 && !error && <div className="empty card">এখনো কোনো আবেদন নেই।</div>}
      <ul className="application-list">
        {items.map((item) => (
          <li className="card application-item" key={item.holdId}>
            <div><strong>{item.className}</strong><span className={`badge ${item.status}`}>{STATUS_LABEL[item.status]}</span></div>
            <button className="secondary" onClick={() => onOpen(item.holdId)}>বিস্তারিত দেখুন</button>
          </li>
        ))}
      </ul>
    </section>
  );
}
