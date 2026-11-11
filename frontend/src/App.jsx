import { useEffect, useState } from 'react';
import { ClassCreate, ClassList, HoldDetail, MyHolds } from './pages.jsx';

const STUDENT_KEY = 'studentId';

export default function App() {
  const [raw, setRaw] = useState(localStorage.getItem(STUDENT_KEY) || '');
  const [view, setView] = useState({ name: 'classes' });
  const validStudent = /^\d{17}$/.test(raw) ? raw : '';

  useEffect(() => {
    localStorage.setItem(STUDENT_KEY, raw);
  }, [raw]);

  return (
    <div className="container">
      <header className="site-header">
        <div>
          <p className="eyebrow">SCHOOL ADMISSION PORTAL</p>
          <h1>স্কুল ভর্তি ব্যবস্থা</h1>
          <p className="subtitle">সহজে ক্লাস নির্বাচন করুন এবং আবেদনের অবস্থা দেখুন।</p>
        </div>
        <label className="student-field">
          জন্ম নিবন্ধন নম্বর (১৭ অঙ্ক)
          <input
            value={raw}
            inputMode="numeric"
            placeholder="যেমন 20152691234567890"
            onChange={(event) => setRaw(event.target.value.replace(/\D/g, '').slice(0, 17))}
          />
        </label>
        {!validStudent && (
          <p className="hint">আবেদন ও আবেদনের তথ্য দেখতে আপনার ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন।</p>
        )}
      </header>

      <nav className="nav-tabs">
        <button className={view.name === 'classes' ? 'active' : ''} onClick={() => setView({ name: 'classes' })}>ক্লাস তালিকা</button>
        <button className={view.name === 'my' || view.name === 'hold' ? 'active' : ''} onClick={() => setView({ name: 'my' })}>আমার আবেদন</button>
        <button className={view.name === 'create' ? 'active' : ''} onClick={() => setView({ name: 'create' })}>ক্লাস তৈরি (স্কুল অ্যাডমিন)</button>
      </nav>

      {view.name === 'classes' && (
        <ClassList studentId={validStudent} onApplied={(holdId) => setView({ name: 'hold', holdId })} />
      )}
      {view.name === 'my' && (
        <MyHolds studentId={validStudent} onOpen={(holdId) => setView({ name: 'hold', holdId })} />
      )}
      {view.name === 'create' && <ClassCreate />}
      {view.name === 'hold' && (
        <HoldDetail holdId={view.holdId} studentId={validStudent} onBack={() => setView({ name: 'my' })} />
      )}
      <footer>School Admission System · React + .NET + PostgreSQL</footer>
    </div>
  );
}
