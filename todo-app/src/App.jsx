import React, { useState, useEffect, useCallback } from 'react';
import ProgressBar from './components/ProgressBar';
import AddTask from './components/AddTask';
import TaskList from './components/TaskList';

export default function App() {
  const [tasks, setTasks] = useState([]);

  const loadTasks = useCallback(async () => {
    const data = await window.electronAPI.getTasks();
    setTasks(data);
  }, []);

  useEffect(() => {
    loadTasks();
  }, [loadTasks]);

  const handleAddTask = async (task) => {
    const newTask = await window.electronAPI.addTask(task);
    setTasks((prev) => [newTask, ...prev]);
  };

  const handleToggle = async (id) => {
    const updated = await window.electronAPI.toggleComplete(id);
    setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
  };

  const handleDelete = async (id) => {
    await window.electronAPI.deleteTask(id);
    setTasks((prev) => prev.filter((t) => t.id !== id));
  };

  const completed = tasks.filter((t) => t.completed).length;

  return (
    <div className="app">
      <header className="app-header">
        <h1>Todo<span>.</span></h1>
      </header>
      <ProgressBar completed={completed} total={tasks.length} />
      <AddTask onAdd={handleAddTask} />
      <TaskList tasks={tasks} onToggle={handleToggle} onDelete={handleDelete} />
    </div>
  );
}
