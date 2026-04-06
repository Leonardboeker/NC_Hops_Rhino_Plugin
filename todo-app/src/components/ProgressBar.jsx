import React from 'react';

export default function ProgressBar({ completed, total }) {
  const percent = total === 0 ? 0 : Math.round((completed / total) * 100);

  return (
    <div className="progress-container">
      <div className="progress-label">
        <span>{completed} of {total} task{total !== 1 ? 's' : ''} completed</span>
        <span>{percent}%</span>
      </div>
      <div className="progress-bar-track">
        <div className="progress-bar-fill" style={{ width: `${percent}%` }} />
      </div>
    </div>
  );
}
