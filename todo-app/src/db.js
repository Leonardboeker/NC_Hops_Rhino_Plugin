const Database = require('better-sqlite3');
const path = require('path');

let db;

function init(userDataPath) {
  const dbPath = path.join(userDataPath, 'tasks.db');
  db = new Database(dbPath);
  db.exec(`
    CREATE TABLE IF NOT EXISTS tasks (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      title TEXT NOT NULL,
      description TEXT,
      due_date TEXT,
      completed INTEGER DEFAULT 0,
      created_at TEXT DEFAULT CURRENT_TIMESTAMP
    )
  `);
}

function getTasks() {
  return db.prepare('SELECT * FROM tasks ORDER BY created_at DESC').all();
}

function addTask({ title, description, due_date }) {
  const stmt = db.prepare(
    'INSERT INTO tasks (title, description, due_date) VALUES (?, ?, ?)'
  );
  const result = stmt.run(title, description || null, due_date || null);
  return db.prepare('SELECT * FROM tasks WHERE id = ?').get(result.lastInsertRowid);
}

function updateTask({ id, title, description, due_date }) {
  db.prepare(
    'UPDATE tasks SET title = ?, description = ?, due_date = ? WHERE id = ?'
  ).run(title, description || null, due_date || null, id);
  return db.prepare('SELECT * FROM tasks WHERE id = ?').get(id);
}

function deleteTask(id) {
  db.prepare('DELETE FROM tasks WHERE id = ?').run(id);
  return { id };
}

function toggleComplete(id) {
  db.prepare('UPDATE tasks SET completed = NOT completed WHERE id = ?').run(id);
  return db.prepare('SELECT * FROM tasks WHERE id = ?').get(id);
}

module.exports = { init, getTasks, addTask, updateTask, deleteTask, toggleComplete };
