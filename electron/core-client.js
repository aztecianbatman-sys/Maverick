const net = require("node:net");

const PIPE_PATH = "\\\\.\\pipe\\MaverickCore";

function request(command, payload = {}, timeoutMs = 10000) {
  return new Promise((resolve, reject) => {
    if (process.platform !== "win32") {
      reject(new Error("Maverick Core currently supports Windows only."));
      return;
    }

    const socket = net.createConnection(PIPE_PATH);
    let buffer = "";
    let settled = false;

    const finish = (fn, value) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      socket.destroy();
      fn(value);
    };

    const timer = setTimeout(() => {
      finish(reject, new Error("Maverick Core request timed out."));
    }, timeoutMs);

    socket.setEncoding("utf8");

    socket.on("connect", () => {
      socket.write(JSON.stringify({ command, ...payload }) + "\n");
    });

    socket.on("data", (chunk) => {
      buffer += chunk;
      const newline = buffer.indexOf("\n");
      if (newline < 0) return;

      const line = buffer.slice(0, newline);
      try {
        const response = JSON.parse(line);
        if (!response.ok) {
          finish(reject, new Error(response.error || "Maverick Core rejected the request."));
          return;
        }
        finish(resolve, response.result);
      } catch {
        finish(reject, new Error("Maverick Core returned invalid JSON."));
      }
    });

    socket.on("error", (error) => {
      finish(reject, new Error("Maverick Core unavailable: " + error.message));
    });

    socket.on("close", () => {
      if (!settled) finish(reject, new Error("Maverick Core connection closed unexpectedly."));
    });
  });
}

module.exports = { request };
