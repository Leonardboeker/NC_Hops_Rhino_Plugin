import React, { useState } from 'react';

export default function AddTask({ onAdd }) {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [dueDate, setDueDate] = useState('');

  const handleSubmit = (e) => {
    e.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) return;
    onAdd({ title: trimmed, description: description.trim(), due_date: dueDate || null });
    setTitle('');
    setDescription('');
    setDueDate('');
  };

  return (
    <div className="add-task-form">
      <h2>New Task</h2>
      <form onSubmit={handleSubmit}>
        <div className="form-row">
          <div className="form-group full">
            <label htmlFor="task-title">Title *</label>
            <input
              id="task-title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="What needs to be done?"
              required
            />
          </div>
          <div className="form-group full">
            <label htmlFor="task-desc">Description</label>
            <textarea
              id="task-desc"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional details..."
            />
          </div>
          <div className="form-group">
            <label htmlFor="task-due">Due Date</label>
            <input
              id="task-due"
              type="date"
              value={dueDate}
              onChange={(e) => setDueDate(e.target.value)}
            />
          </div>
        </div>
        <div className="form-actions">
          <button type="submit" className="btn-primary">Add Task</button>
        </div>
      </form>
    </div>
  );
}
