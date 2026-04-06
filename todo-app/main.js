const { app, BrowserWindow, ipcMain, shell } = require('electron');
const path = require('path');
const db = require('./src/db');
const { addTaskToCalendar } = require('./src/googleCalendar');

let mainWindow;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 900,
    height: 700,
    minWidth: 600,
    minHeight: 500,
    backgroundColor: '#1a1a2e',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
    show: false,
  });

  mainWindow.loadFile(path.join(__dirname, 'dist', 'index.html'));

  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
  });
}

app.whenReady().then(() => {
  const userDataPath = app.getPath('userData');
  db.init(userDataPath);
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

ipcMain.handle('get-tasks', () => {
  return db.getTasks();
});

ipcMain.handle('add-task', (event, task) => {
  return db.addTask(task);
});

ipcMain.handle('update-task', (event, task) => {
  return db.updateTask(task);
});

ipcMain.handle('delete-task', (event, id) => {
  return db.deleteTask(id);
});

ipcMain.handle('toggle-complete', (event, id) => {
  return db.toggleComplete(id);
});

ipcMain.handle('add-to-calendar', async (event, task) => {
  const userDataPath = app.getPath('userData');
  return addTaskToCalendar(task, userDataPath, shell);
});
