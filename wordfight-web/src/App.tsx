import React, { useState, useEffect } from "react";
import "./App.css";
import {
  AnswerStatus,
  Client,
  ClientboundPacket,
  Prompt,
  Player,
} from "./client/Client";
import { makeAutoObservable } from "mobx";
import { observer } from "mobx-react";
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
} from "@tanstack/react-table";

// #region language
type translation = {
  leaderboard: string;
  waiting: string;
  opponent: string;
  you: string;
  cancel: string;
  name: string;
  correctAnswers: string;
  totalAnswers: string;
  join: string;
  answerStatuses: { [key in AnswerStatus]: string };
  connecting: string;
  playingSolo: string;
  logoff: string;
};

type language = "EN" | "CZ";

const translations: { [key in language]: translation } = {
  EN: {
    leaderboard: "Leaderboard",
    waiting: "Searching for an opponent",
    opponent: "Opponent",
    you: "You",
    cancel: "Cancel matchmaking",
    name: "Name",
    correctAnswers: "Correct answers",
    totalAnswers: "Total answers",
    join: "Join",
    answerStatuses: {
      Correct: "Correct",
      Incorrect: "Incorrect",
      Unanswered: "Unanswered",
    },
    connecting: "Connecting...",
    playingSolo: "Play alone",
    logoff: "Log off",
  },
  CZ: {
    leaderboard: "Žebříček",
    waiting: "Hledání protivníka",
    opponent: "Protivník",
    you: "Ty",
    cancel: "Zrušit hledání",
    name: "Jméno",
    correctAnswers: "Správné odpovědi",
    totalAnswers: "Celkem odpovědí",
    join: "Připojit se",
    answerStatuses: {
      Correct: "Správně",
      Incorrect: "Špatně",
      Unanswered: "Nezodpovězeno",
    },
    connecting: "Připojování...",
    playingSolo: "Hrát sám",
    logoff: "Odhlásit se",
  },
};

let curLang = translations.CZ;

// #endregion

let hideStates: { [key: string]: boolean } = makeAutoObservable({
  joinDiv: false,
  connectingDiv: true,
});

const HideStyles = observer(() => {
  // var style = document.getElementById("balls");
  // if (!style) return null;
  var css = "";
  for (const key in hideStates) {
    if (hideStates[key]) css += `.${key} { display: none; }`;
  }
  // style.innerHTML = css;
  // return null;
  return <style>{css}</style>;
});

let thisIsHell: (packet: ClientboundPacket) => void | undefined;

let gameState = makeAutoObservable({
  waiting: false,
  opponentName: "",
  prompt: null as null | Prompt,
  answer: "",
  answerStatus: "Unanswered" as AnswerStatus,
  leaderboard: [] as Player[],
  playingSolo: false,
});

let client = new Client((packet) => {
  console.log(packet);
  if (thisIsHell) thisIsHell(packet);
  if (packet.type === "joined") {
    client.send({ type: "start", playSolo: gameState.playingSolo });
    gameState.waiting = true;
  } else if (packet.type === "match") {
    gameState.waiting = false;
    gameState.opponentName = packet.otherPlayerName;
  } else if (packet.type === "prompt") {
    gameState.answerStatus = "Unanswered";
    gameState.prompt = packet.prompt;
    gameState.answer = packet.prompt.answer;
  } else if (packet.type === "answerStatus") {
    gameState.answerStatus = packet.answerStatus;
  } else if (packet.type === "matchEnd") {
    gameState.opponentName = "";
    gameState.prompt = null;
    gameState.answerStatus = "Unanswered";
    client.send({ type: "start", playSolo: gameState.playingSolo });
    gameState.waiting = true;
  } else if (packet.type === "leaderboard") {
    gameState.leaderboard = packet.players;
  }
});

const WaitingView = observer(() => {
  const [dots, setDots] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setDots((dots) => (dots === 3 ? 0 : dots + 1));
    }, 500);
    return () => clearInterval(interval);
  });

  if (!gameState.waiting) return <></>;

  return (
    <div className="waitingDiv page">
      <div
        style={{
          borderLeft: "10px solid red",
          background: "rgb(255, 200, 200)",
          width: "calc(100% - 10px)",
          height: 34,
          textAlign: "left",
        }}
      >
        {curLang.waiting}
        {".".repeat(dots)}
      </div>
      <div
        style={{
          borderLeft: "10px solid blue",
          background: "rgb(200, 200, 255)",
          width: "calc(100% - 10px)",
          height: 34,
          textAlign: "left",
        }}
      >
        {localStorage.getItem("name")}(YOU)
      </div>
      <div
        className="button"
        onClick={() => {
          localStorage.removeItem("name");
          gameState.waiting = false;
          hideStates.joinDiv = false;
          client.disconnect();
        }}
      >
        {curLang.cancel}
      </div>
    </div>
  );
});

const OpponentView = observer(() => {
  if (gameState.opponentName === "") return <></>;
  return (
    <>
      <div
        style={{
          borderLeft: "10px solid red",
          background: "rgb(255, 150, 150)",
          width: "calc(100% - 10px)",
          height: 34,
          textAlign: "left",
        }}
      >
        {gameState.opponentName}
      </div>
      <div
        style={{
          borderLeft: "10px solid blue",
          background: "rgb(150, 150, 255)",
          width: "calc(100% - 10px)",
          height: 34,
          textAlign: "left",
        }}
      >
        {localStorage.getItem("name")}({curLang.you})
      </div>
    </>
  );
});

const AnswerView = observer(() => {
  if (gameState.prompt === null) return <></>;

  const prompt = gameState.prompt as Prompt;
  return (
    <div className="answerDiv">
      <h2>{prompt.text}</h2>
      {prompt.options.map((answer, i) => (
        <button
          className="answerButton button"
          key={i}
          style={{
            backgroundColor:
              i < 4
                ? ["#f88", "#8f8", "#88f", "#ffddbb"][i]
                : ["#ffffff", "#ccccff"][i % 2],
          }}
          onClick={() => {
            client.send({ type: "answer", answer: answer });
            gameState.prompt = null;
          }}
        >
          {answer}
        </button>
      ))}
    </div>
  );
});

const AnswerStatusView = observer(() => {
  if (gameState.answerStatus === "Unanswered") return <></>;
  return (
    <div className={"answerStatusDiv info"}>
      <p>{curLang.answerStatuses[gameState.answerStatus]}</p>
      {gameState.answerStatus === "Incorrect" ? <p>{gameState.answer}</p> : <></>}
    </div>
  );
});

const LeaderboardView = observer(() => {
  const data = gameState.leaderboard;
  const columnHelper = createColumnHelper<Player>();
  let table = useReactTable({
    data,
    columns: [
      columnHelper.accessor("name", {
        header: () => curLang.name,
      }),
      columnHelper.accessor("correctAnswers", {
        header: () => curLang.correctAnswers,
      }),
      columnHelper.accessor("totalAnswers", {
        header: () => curLang.totalAnswers,
      }),
    ],
    getCoreRowModel: getCoreRowModel(),
  });
  if (data.length === 0 || !data) return <></>;
  return (
    <div>
      <h2>{curLang.leaderboard}</h2>
      <table>
        <thead>
          {table.getHeaderGroups().map((headerGroup) => (
            <tr key={headerGroup.id}>
              {headerGroup.headers.map((header) => (
                <th key={header.id}>
                  {header.isPlaceholder
                    ? null
                    : flexRender(
                        header.column.columnDef.header,
                        header.getContext()
                      )}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((row) => (
            <tr key={row.id}>
              {row.getVisibleCells().map((cell) => (
                <td key={cell.id}>
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
});

function App() {
  var [name, setName] = useState(
    (localStorage.getItem("name") as string) || ""
  );
  const buttonClick = () => {
    client.connect(name);
    localStorage.setItem("name", name);
  };
  useEffect(() => {
    if (name !== "" && !hideStates["joinDiv"]) {
      buttonClick();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  client.onConnect = () => {
    hideStates["connectingDiv"] = true;
  };
  thisIsHell = (packet: ClientboundPacket) => {
    // var style = document.getElementById("balls");
    // if (!style) console.log("WHAT THE HELL");
    // else {
      hideStates["joinDiv"] = true;
    // }
  };
  return (
    <div className="App" style={{ height: "100%" }}>
      <div className="joinDiv page">
        <div>
          <input
            className="input"
            type="text"
            placeholder={curLang.name}
            value={name}
            maxLength={48}
            onChange={(e) => {
              // check name is not empty or whitespace
              if (e.target.value.trim() === "") return;
              setName(e.target.value);
            }}
          />
          <button className="button" onClick={buttonClick}>
            {curLang.join}
          </button>

          <input type="checkbox" onChange={(e) => {
            gameState.playingSolo = e.target.checked;
          }}/>
          <label>{curLang.playingSolo}</label>
        </div>
      </div>
      <div className="connectingDiv">
        <p>{curLang.connecting}</p>
      </div>

      <WaitingView />
      <OpponentView />
      <AnswerView />
      <AnswerStatusView />

      <LeaderboardView />
      <HideStyles />

      <button className="button" onClick={() => {
        localStorage.removeItem("name");
        // gameState.waiting = false;
        // hideStates.joinDiv = false;
        // client.disconnect();

        window.location.reload();
      }
      } style={{bottom: "0"}}>{curLang.logoff}</button>
    </div>
  );
}

export default App;
