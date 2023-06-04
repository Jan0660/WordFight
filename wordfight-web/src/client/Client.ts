export class Client {
    private socket: WebSocket | undefined;
    private onPacket: (packet: ClientboundPacket) => void;
    public onConnect: () => void = () => {};

    constructor(onPacket: (packet: ClientboundPacket) => void) {
        this.onPacket = onPacket;
    }

    connect(name: string) {
        this.socket = new WebSocket(process.env.NODE_ENV === "development" ? "ws://localhost:5152/ws" : "wss://wfapi.jan0660.dev/ws");
        this.socket.onopen = () => {
            this.onConnect();
            this.send({ type: "join", name });
        }
        this.socket.onmessage = (event) => {
            const packet = JSON.parse(event.data) as ClientboundPacket;
            this.onPacket(packet);
        }
    }

    send(packet: ServerboundPacket) {
        if (!this.socket)
            throw new Error("Socket not connected");
        // wait for socket to be ready, because even though it's open, it might not be ready ????
        if (this.socket.readyState !== 1) {
            const timeout = () => setTimeout(() => {
                if (this.socket?.readyState !== 1)
                    timeout();
                else
                    this.socket.send(JSON.stringify(packet));
            }, 50);
            timeout();
            return;
        }
        this.socket.send(JSON.stringify(packet));
    }

    disconnect() {
        this.socket?.close()
    }
}

export type Prompt = {
    text: string,
    answer: string,
    options: string[],
};

export type AnswerStatus = "Unanswered" | "Correct" | "Incorrect";

export type Player = {
    name: string,
    // status
    correctAnswers: number,
    incorrectAnswers: number,
    totalAnswers: number,
};

//#region server bound packets
export type JoinPacket = {
    type: "join",
    name: string,
};

export type StartPacket = {
    type: "start",
    playSolo: boolean,
};

export type AnswerPacket = {
    type: "answer",
    answer: string,
}

export type ServerboundPacket = | JoinPacket | StartPacket | AnswerPacket;

//#endregion

//#region client bount packets
export type JoinedPacket = {
    type: "joined",
    // todo: player
};

export type MatchPacket = {
    type: "match",
    otherPlayerName: string,
}

export type PromptPacket = {
    type: "prompt",
    prompt: Prompt,
}

export type AnswerStatusPacket = {
    type: "answerStatus",
    answerStatus: AnswerStatus,
}

export type MatchEndPacket = {
    type: "matchEnd",
    winnerName: string,
}

export type LeaderboardPacket = {
    type: "leaderboard",
    players: Player[],
}

export type ClientboundPacket = | JoinedPacket | MatchPacket | PromptPacket | AnswerStatusPacket | MatchEndPacket | LeaderboardPacket;
//#endregion
