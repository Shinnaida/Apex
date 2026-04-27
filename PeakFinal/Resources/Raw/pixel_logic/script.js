const CELL_UNKNOWN = 0;
const CELL_FILLED = 1;
const CELL_MARKED = 2;

const puzzles = [
  {
    id: "easy-5-heart",
    name: "5x5 Easy Heart",
    difficulty: "Easy",
    solution: [
      [0,1,0,1,0],
      [1,1,1,1,1],
      [1,1,1,1,1],
      [0,1,1,1,0],
      [0,0,1,0,0]
    ]
  },
  {
    id: "easy-8-rocket",
    name: "8x8 Easy Rocket",
    difficulty: "Easy",
    solution: [
      [0,0,0,1,1,0,0,0],
      [0,0,1,1,1,1,0,0],
      [0,1,1,1,1,1,1,0],
      [0,0,0,1,1,0,0,0],
      [0,0,1,1,1,1,0,0],
      [0,1,1,0,0,1,1,0],
      [1,1,0,0,0,0,1,1],
      [0,0,1,0,0,1,0,0]
    ]
  },
  {
    id: "medium-10-smile",
    name: "10x10 Medium Smile",
    difficulty: "Medium",
    solution: [
      [0,0,1,1,1,1,1,1,0,0],
      [0,1,1,0,0,0,0,1,1,0],
      [1,1,0,1,0,0,1,0,1,1],
      [1,1,0,0,0,0,0,0,1,1],
      [1,1,0,1,0,0,1,0,1,1],
      [1,1,0,0,1,1,0,0,1,1],
      [0,1,1,0,0,0,0,1,1,0],
      [0,0,1,1,1,1,1,1,0,0],
      [0,0,0,1,1,1,1,0,0,0],
      [0,0,0,0,1,1,0,0,0,0]
    ]
  },
  {
    id: "medium-10-lightbulb",
    name: "10x10 Medium Bulb",
    difficulty: "Medium",
    solution: [
      [0,0,0,1,1,1,1,0,0,0],
      [0,0,1,1,1,1,1,1,0,0],
      [0,1,1,1,1,1,1,1,1,0],
      [0,1,1,1,1,1,1,1,1,0],
      [0,0,1,1,1,1,1,1,0,0],
      [0,0,0,1,1,1,1,0,0,0],
      [0,0,0,1,1,1,1,0,0,0],
      [0,0,1,1,1,1,1,1,0,0],
      [0,0,0,1,1,1,1,0,0,0],
      [0,0,0,1,1,1,1,0,0,0]
    ]
  },
  {
    id: "hard-15-cat",
    name: "15x15 Hard Cat",
    difficulty: "Hard",
    solution: [
      [0,0,0,1,0,0,0,0,0,0,0,1,0,0,0],
      [0,0,1,1,1,0,0,0,0,0,1,1,1,0,0],
      [0,1,1,1,1,1,0,0,0,1,1,1,1,1,0],
      [0,1,1,1,1,1,0,0,0,1,1,1,1,1,0],
      [0,0,1,1,1,0,0,0,0,0,1,1,1,0,0],
      [0,0,0,1,0,0,1,1,1,0,0,1,0,0,0],
      [0,0,1,1,1,0,1,1,1,0,1,1,1,0,0],
      [0,1,1,1,1,1,1,1,1,1,1,1,1,1,0],
      [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1],
      [1,1,1,0,1,1,1,1,1,1,1,0,1,1,1],
      [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1],
      [0,1,1,0,0,1,1,1,1,1,0,0,1,1,0],
      [0,0,1,1,1,0,0,1,0,0,1,1,1,0,0],
      [0,0,0,1,1,1,0,0,0,1,1,1,0,0,0],
      [0,0,0,0,1,1,1,0,1,1,1,0,0,0,0]
    ]
  }
];

const state = {
  puzzleIndex: 0,
  board: [],
  rowClues: [],
  colClues: [],
  timerId: null,
  elapsedSeconds: 0,
  paused: false,
  solved: false,
  hintCount: 3,
  touchMode: CELL_FILLED,
  dragging: false,
  dragMode: CELL_UNKNOWN
};

const elements = {
  puzzleSelect: document.getElementById("puzzleSelect"),
  board: document.getElementById("board"),
  timer: document.getElementById("timer"),
  mistakes: document.getElementById("mistakes"),
  message: document.getElementById("message"),
  modal: document.getElementById("modal"),
  modalText: document.getElementById("modalText"),
  modalCloseBtn: document.getElementById("modalCloseBtn"),
  restartBtn: document.getElementById("restartBtn"),
  checkBtn: document.getElementById("checkBtn"),
  hintBtn: document.getElementById("hintBtn"),
  hintCount: document.getElementById("hintCount"),
  hintStatus: document.getElementById("hintStatus"),
  pauseBtn: document.getElementById("pauseBtn"),
  revealBtn: document.getElementById("revealBtn"),
  themeBtn: document.getElementById("themeBtn"),
  soundBtn: document.getElementById("soundBtn"),
  bestTime: document.getElementById("bestTime"),
  fillModeBtn: document.getElementById("fillModeBtn"),
  xModeBtn: document.getElementById("xModeBtn"),
  modeStatus: document.getElementById("modeStatus")
};

const audioState = { enabled: true, ctx: null };

function createEmptyBoard(size) {
  return Array.from({ length: size }, () => Array(size).fill(CELL_UNKNOWN));
}

function cloneGrid(grid) {
  return grid.map(row => [...row]);
}

function computeClues(grid) {
  const linesToClues = (lines) => lines.map(line => {
    const groups = [];
    let run = 0;
    line.forEach(value => {
      if (value === 1) run += 1;
      else if (run > 0) {
        groups.push(run);
        run = 0;
      }
    });
    if (run > 0) groups.push(run);
    return groups.length ? groups : [0];
  });

  const rows = linesToClues(grid);
  const cols = linesToClues(grid[0].map((_, c) => grid.map(row => row[c])));
  return { rows, cols };
}

function getCurrentPuzzle() {
  return puzzles[state.puzzleIndex];
}

function formatTime(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

function bestTimeKey(puzzle) {
  return `pixel-logic-best-${puzzle.id}`;
}

function getBestTime(puzzle) {
  const raw = localStorage.getItem(bestTimeKey(puzzle));
  return raw ? Number(raw) : null;
}

function setBestTime(puzzle, seconds) {
  const current = getBestTime(puzzle);
  if (current === null || seconds < current) {
    localStorage.setItem(bestTimeKey(puzzle), String(seconds));
  }
}

function setMessage(text) {
  elements.message.textContent = text;
}

function updateStatus() {
  const puzzle = getCurrentPuzzle();
  elements.timer.textContent = formatTime(state.elapsedSeconds);
  elements.hintCount.textContent = String(state.hintCount);
  elements.hintStatus.textContent = String(state.hintCount);
  elements.modeStatus.textContent = state.touchMode === CELL_FILLED ? "Fill" : "X";
  elements.bestTime.textContent = getBestTime(puzzle) !== null ? formatTime(getBestTime(puzzle)) : "--:--";
}

function stopTimer() {
  if (state.timerId) {
    clearInterval(state.timerId);
    state.timerId = null;
  }
}

function startTimer() {
  stopTimer();
  state.timerId = setInterval(() => {
    if (!state.paused && !state.solved) {
      state.elapsedSeconds += 1;
      updateStatus();
    }
  }, 1000);
}

function playTone(freq, duration = 0.08, type = "sine", gain = 0.02) {
  if (!audioState.enabled) return;
  const ctx = audioState.ctx || new (window.AudioContext || window.webkitAudioContext)();
  audioState.ctx = ctx;
  const oscillator = ctx.createOscillator();
  const gainNode = ctx.createGain();
  oscillator.type = type;
  oscillator.frequency.value = freq;
  gainNode.gain.value = gain;
  oscillator.connect(gainNode);
  gainNode.connect(ctx.destination);
  oscillator.start();
  oscillator.stop(ctx.currentTime + duration);
}

function initSelector() {
  elements.puzzleSelect.innerHTML = puzzles.map((puzzle, index) =>
    `<option value="${index}">${puzzle.name} · ${puzzle.difficulty}</option>`
  ).join("");
}

function resetPuzzle(index = state.puzzleIndex) {
  state.puzzleIndex = index;
  const puzzle = getCurrentPuzzle();
  state.board = createEmptyBoard(puzzle.solution.length);
  const clues = computeClues(puzzle.solution);
  state.rowClues = clues.rows;
  state.colClues = clues.cols;
  state.elapsedSeconds = 0;
  state.hintCount = 3;
  state.paused = false;
  state.solved = false;
  elements.pauseBtn.textContent = "Pause";
  document.body.classList.remove("solved");
  renderBoard();
  updateStatus();
  startTimer();
  setMessage(`Solve ${puzzle.name}. ${puzzle.difficulty} difficulty.`);
}

function getCellVisualState(r, c) {
  return state.board[r][c];
}

function cycleState(current, forceState = null) {
  if (forceState !== null) return forceState;
  if (current === CELL_UNKNOWN) return CELL_FILLED;
  if (current === CELL_FILLED) return CELL_MARKED;
  return CELL_UNKNOWN;
}

function applyCellState(r, c, nextState) {
  if (state.solved || state.paused) return;
  state.board[r][c] = nextState;
  renderBoard();
}

function handlePrimaryAction(r, c) {
  const next = cycleState(state.board[r][c]);
  applyCellState(r, c, next);
  playTone(next === CELL_FILLED ? 440 : next === CELL_MARKED ? 280 : 220, 0.05, "triangle", 0.015);
  checkSolved();
}

function handleAlternateAction(r, c) {
  const current = state.board[r][c];
  const next = current === CELL_MARKED ? CELL_UNKNOWN : CELL_MARKED;
  applyCellState(r, c, next);
  playTone(260, 0.05, "square", 0.012);
  checkSolved();
}

function renderBoard() {
  const puzzle = getCurrentPuzzle();
  const size = puzzle.solution.length;
  const rowDepth = Math.max(...state.rowClues.map(clue => clue.length));
  const colDepth = Math.max(...state.colClues.map(clue => clue.length));
  const cellSize = size >= 15 ? 28 : size >= 10 ? 34 : 42;

  elements.board.style.gridTemplateColumns = `repeat(${colDepth}, ${cellSize}px) repeat(${size}, ${cellSize}px)`;
  elements.board.style.gridTemplateRows = `repeat(${rowDepth}, ${cellSize}px) repeat(${size}, ${cellSize}px)`;
  elements.board.innerHTML = "";

  for (let r = 0; r < rowDepth; r++) {
    for (let c = 0; c < colDepth; c++) {
      const corner = document.createElement("div");
      corner.className = "corner";
      elements.board.appendChild(corner);
    }

    for (let c = 0; c < size; c++) {
      const clue = document.createElement("div");
      clue.className = "clue";
      const clueValues = state.colClues[c];
      const value = clueValues[rowDepth - clueValues.length + r];
      clue.textContent = value ? String(value) : "";
      clue.dataset.col = c;
      elements.board.appendChild(clue);
    }
  }

  for (let r = 0; r < size; r++) {
    for (let c = 0; c < colDepth; c++) {
      const clue = document.createElement("div");
      clue.className = "clue";
      const clueValues = state.rowClues[r];
      const value = clueValues[colDepth - clueValues.length + c];
      clue.textContent = value ? String(value) : "";
      clue.dataset.row = r;
      elements.board.appendChild(clue);
    }

    for (let c = 0; c < size; c++) {
      const cell = document.createElement("button");
      cell.type = "button";
      cell.className = "cell";
      cell.dataset.row = r;
      cell.dataset.col = c;
      cell.setAttribute("aria-label", `Row ${r + 1} column ${c + 1}`);

      const value = getCellVisualState(r, c);
      if (value === CELL_FILLED) cell.classList.add("filled");
      if (value === CELL_MARKED) cell.classList.add("marked");

      cell.addEventListener("click", (event) => {
        event.preventDefault();
        handlePrimaryAction(r, c);
      });
      cell.addEventListener("contextmenu", (event) => {
        event.preventDefault();
        handleAlternateAction(r, c);
      });
      cell.addEventListener("pointerenter", () => highlightLine(r, c));
      cell.addEventListener("pointerleave", clearHighlights);
      cell.addEventListener("pointerdown", (event) => onPointerDown(event, r, c));
      cell.addEventListener("pointerenter", () => onPointerEnter(r, c));
      cell.addEventListener("pointerup", onPointerUp);
      elements.board.appendChild(cell);
    }
  }

  elements.board.oncontextmenu = (event) => event.preventDefault();
}

function onPointerDown(event, r, c) {
  if (state.paused || state.solved) return;
  event.preventDefault();
  state.dragging = true;
  state.dragMode = event.pointerType === "touch"
    ? state.touchMode
    : (event.button === 2 ? CELL_MARKED : CELL_FILLED);
  applyDragCell(r, c);
}

function onPointerEnter(r, c) {
  if (!state.dragging) return;
  applyDragCell(r, c);
}

function onPointerUp() {
  if (!state.dragging) return;
  state.dragging = false;
  checkSolved();
}

function applyDragCell(r, c) {
  const desired = state.dragMode;
  const current = state.board[r][c];
  if (current !== desired) {
    state.board[r][c] = desired;
    renderBoard();
  }
}

window.addEventListener("pointerup", onPointerUp);

function highlightLine(r, c) {
  clearHighlights();
  document.querySelectorAll(`[data-row="${r}"], [data-col="${c}"]`).forEach(node => node.classList.add("hovered"));
}

function clearHighlights() {
  document.querySelectorAll(".hovered").forEach(node => node.classList.remove("hovered"));
}

function compareBoards(player, solution) {
  for (let r = 0; r < solution.length; r++) {
    for (let c = 0; c < solution.length; c++) {
      const expectedFilled = solution[r][c] === 1;
      const playerFilled = player[r][c] === CELL_FILLED;
      if (expectedFilled !== playerFilled) {
        return false;
      }
    }
  }
  return true;
}

function checkSolved() {
  if (!compareBoards(state.board, getCurrentPuzzle().solution)) return false;
  state.solved = true;
  stopTimer();
  setBestTime(getCurrentPuzzle(), state.elapsedSeconds);
  updateStatus();
  document.body.classList.add("solved");
  playTone(740, 0.12, "triangle", 0.03);
  setTimeout(() => playTone(980, 0.16, "triangle", 0.03), 120);
  const score = Math.max(100, Math.round(1500 - state.elapsedSeconds * 3 - (3 - state.hintCount) * 90));
  openModal(`Solved in ${formatTime(state.elapsedSeconds)}. Score ${score}.`);
  notifyHost("complete", { score, seconds: state.elapsedSeconds, puzzleId: getCurrentPuzzle().id });
  return true;
}

function openModal(text) {
  elements.modalText.textContent = text;
  elements.modal.classList.remove("hidden");
}

function closeModal() {
  elements.modal.classList.add("hidden");
  resetPuzzle(state.puzzleIndex);
}

function giveHint() {
  if (state.hintCount <= 0 || state.solved) {
    setMessage("No hints left.");
    return;
  }

  const puzzle = getCurrentPuzzle();
  const candidates = [];
  for (let r = 0; r < puzzle.solution.length; r++) {
    for (let c = 0; c < puzzle.solution.length; c++) {
      const shouldFill = puzzle.solution[r][c] === 1;
      const current = state.board[r][c];
      if ((shouldFill && current !== CELL_FILLED) || (!shouldFill && current === CELL_UNKNOWN)) {
        candidates.push({ r, c, value: shouldFill ? CELL_FILLED : CELL_MARKED });
      }
    }
  }

  if (!candidates.length) {
    setMessage("No useful hint available right now.");
    return;
  }

  const pick = candidates[Math.floor(Math.random() * candidates.length)];
  state.board[pick.r][pick.c] = pick.value;
  state.hintCount -= 1;
  renderBoard();
  updateStatus();
  playTone(520, 0.08, "sine", 0.02);
  setMessage(`Hint used on row ${pick.r + 1}, column ${pick.c + 1}.`);
  checkSolved();
}

function checkProgress() {
  const puzzle = getCurrentPuzzle();
  let incorrect = 0;
  document.querySelectorAll(".cell").forEach(cell => {
    cell.classList.remove("incorrect");
    const r = Number(cell.dataset.row);
    const c = Number(cell.dataset.col);
    const value = state.board[r][c];
    const shouldFill = puzzle.solution[r][c] === 1;
    if ((value === CELL_FILLED && !shouldFill) || (value === CELL_MARKED && shouldFill)) {
      incorrect += 1;
      cell.classList.add("incorrect");
    }
  });
  setMessage(incorrect ? `${incorrect} incorrect marks highlighted.` : "So far so good.");
  elements.mistakes.textContent = String(incorrect);
}

function revealSolution() {
  if (!confirm("Reveal the full solution? This will end the current puzzle.")) return;
  const puzzle = getCurrentPuzzle();
  state.board = cloneGrid(puzzle.solution).map(row => row.map(value => value === 1 ? CELL_FILLED : CELL_MARKED));
  state.solved = true;
  stopTimer();
  renderBoard();
  setMessage("Solution revealed.");
  notifyHost("revealed", { puzzleId: puzzle.id });
}

function togglePause() {
  state.paused = !state.paused;
  elements.pauseBtn.textContent = state.paused ? "Resume" : "Pause";
  setMessage(state.paused ? "Puzzle paused." : "Back to solving.");
}

function toggleTheme() {
  document.body.classList.toggle("dark");
  elements.themeBtn.textContent = document.body.classList.contains("dark") ? "Light" : "Dark";
}

function toggleSound() {
  audioState.enabled = !audioState.enabled;
  elements.soundBtn.textContent = audioState.enabled ? "Sound On" : "Sound Off";
}

function setTouchMode(mode) {
  state.touchMode = mode;
  elements.fillModeBtn.classList.toggle("active", mode === CELL_FILLED);
  elements.xModeBtn.classList.toggle("active", mode === CELL_MARKED);
  updateStatus();
}

function notifyHost(type, payload) {
  try {
    const encoded = encodeURIComponent(JSON.stringify(payload));
    window.location.href = `pixellogic://${type}?data=${encoded}`;
  } catch (_) {
    // Ignore if host bridge is unavailable.
  }
}

function bindEvents() {
  elements.puzzleSelect.addEventListener("change", (event) => resetPuzzle(Number(event.target.value)));
  elements.restartBtn.addEventListener("click", () => resetPuzzle(state.puzzleIndex));
  elements.checkBtn.addEventListener("click", checkProgress);
  elements.hintBtn.addEventListener("click", giveHint);
  elements.pauseBtn.addEventListener("click", togglePause);
  elements.revealBtn.addEventListener("click", revealSolution);
  elements.themeBtn.addEventListener("click", toggleTheme);
  elements.soundBtn.addEventListener("click", toggleSound);
  elements.modalCloseBtn.addEventListener("click", closeModal);
  elements.fillModeBtn.addEventListener("click", () => setTouchMode(CELL_FILLED));
  elements.xModeBtn.addEventListener("click", () => setTouchMode(CELL_MARKED));
}

initSelector();
bindEvents();
resetPuzzle(0);
