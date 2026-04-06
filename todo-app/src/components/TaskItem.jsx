import React, { useState } from 'react';

export default function TaskItem({ task, onToggle, onDelete }) {
  const [calendarStatus, setCalendarStatus] = useState(null);
  const [loading, setLoading] = useState(false);

  const handleCalendar = async () => {
    setLoading(true);
    setCalendarStatus(null);
    try {
      await window.electronAPI.addToCalendar(task);
      setCalendarStatus({ type: 'success', message: 'Added to Google Calendar' });
    } catch (err) {
      setCalendarStatus({ type: 'error', message: err.message || 'Failed to add' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={`task-item${task.completed ? ' completed' : ''}`}>
      <input
        type="checkbox"
        className="task-checkbox"
        checked={!!task.completed}
        onChange={() => onToggle(task.id)}
      />
      <div className="task-content">
        <div className="task-title">{task.title}</div>
        {task.description && (
          <div className="task-description">{task.description}</div>
        )}
        <div className="task-meta">
          {task.due_date && (
            <span className="task-due">{task.due_date}</span>
          )}
          {calendarStatus && (
            <span className={`calendar-feedback ${calendarStatus.type}`}>
              {calendarStatus.message}
            </span>
          )}
        </div>
      </div>
      <div className="task-actions">
        <button
          className="btn-icon btn-calendar"
          onClick={handleCalendar}
          disabled={loading}
          title="Add to Google Calendar"
        >
          {loading ? '...' : 'Cal'}
        </button>
        <button
          className="btn-icon btn-delete"
          onClick={() => onDelete(task.id)}
          title="Delete task"
        >
          &#x2715;
        </button>
      </div>
    </div>
  );
}
