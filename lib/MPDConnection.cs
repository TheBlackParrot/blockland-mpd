if($Pref::MPD::PingInterval $= "") { $Pref::MPD::PingInterval = 59; }

$MPD::Version = "";

function initMPDConnection(%addr) {
	%obj = new TCPObject(MPDTCPObject) {
		lines = "";
	};
	%obj.lines = new GuiTextListCtrl(MPDTCPLines);
	%obj.lines.connection = %obj;

	%obj.connect(%addr);
	%obj.addr = %addr;

	return %obj;
}

function MPDTCPObject::ping(%this) {
	if(%this.connected) {
		%this.send("ping\r\n");
	}
}

function MPDTCPObject::onConnected(%this) {
	cancel(%this.connectRetryLoop);
	%this.connected = true;

	%this.ping();

	echo("Connected to the MPD instance on" SPC %this.addr);
}

function MPDTCPObject::onConnectFailed(%this) {
	cancel(%this.connectRetryLoop);
	echo("Trying to connect to the MPD instance on" SPC %this.addr SPC "(failed to connect)...");
	%this.connected = false;
	%this.connectRetryLoop = %this.schedule(1000, connect, %this.addr);
}

function MPDTCPObject::onDisconnect(%this) {
	cancel(%this.connectRetryLoop);
	echo("Trying to connect to the MPD instance on" SPC %this.addr SPC "(disconnected)...");
	%this.connected = false;
	%this.connectRetryLoop = %this.schedule(1000, connect, %this.addr);
}

function MPDTCPObject::line(%this, %line) {
	%this.lines.send(%line);
}

function MPDTCPLines::send(%this, %data) {
	%this.addRow(getSimTime(), %data);
	if(!isEventPending(%this.checkToSendSched)) {
		%this.checkToSend();
	}
}

function MPDTCPLines::checkToSend(%this) {
	if(%this.rowCount() <= 0) {
		return;
	}

	%this.checkToSendSched = %this.schedule(33, checkToSend);

	%data = %this.getRowText(0);
	%this.removeRow(0);

	%this.connection.send(%data @ "\r\n");
	echo("\c5[SENT]\c0" SPC %data);
}

function MPDTCPObject::onLine(%this, %line) {
	%line = trim(%line);
	echo("[MPD]" SPC %line);
	
	%func = "_MPD" @ getWord(%line, 0);
	if(isFunction(MPDTCPObject, %func)) {
		%this.call(%func, getWords(%line, 1));
	} else {
		switch$(%this.lastCmd) {
			case "status":
			case "stats":
			case "replay_gain_status":
				%v = strReplace(getWord(%line, 0), ":", "");
				%this.v[%v] = getWords(%line, 1);

			case "currentsong":
				%v = strReplace(getWord(%line, 0), ":", "");
				%this.cs[%v] = getWords(%line, 1);
		}
	}
}

function MPDTCPObject::_MPDOK(%this, %args) {
	if(getWord(%args, 0) $= "MPD") {
		%this.apiVersion = getWord(%args, 1);
	}

	%this.lastCmd = "";

	cancel(%this.pingSched);
	%this.pingSched = %this.schedule($Pref::MPD::PingInterval*1000, ping);

	if(%this.callback !$= "") {
		switch(%this.callbackArgCount) {
			case 0: %this.call(%this.callback);
			case 1: %this.call(%this.callback,
				%this.callbackArg[0]);
			case 2: %this.call(%this.callback,
				%this.callbackArg[0],
				%this.callbackArg[1]);
			case 3: %this.call(%this.callback,
				%this.callbackArg[0],
				%this.callbackArg[1],
				%this.callbackArg[2]);
			case 4: %this.call(%this.callback,
				%this.callbackArg[0],
				%this.callbackArg[1],
				%this.callbackArg[2],
				%this.callbackArg[3]);
			case 5: %this.call(%this.callback,
				%this.callbackArg[0],
				%this.callbackArg[1],
				%this.callbackArg[2],
				%this.callbackArg[3],
				%this.callbackArg[4]);
		}

		%this.callback = "";
		for(%idx = 0; %idx < %this.callbackArgCount; %idx++) {
			%this.callbackArg[%idx] = "";
		}
		%this.callbackArgCount = "";
	}
}

function MPDTCPObject::_MPDACK(%this, %args) {
	error("MPD error -- last command was" SPC %this.lastCmd @ "\r\n" @ %args);

	%this.lastCmd = "";

	cancel(%this.pingSched);
	%this.pingSched = %this.schedule($Pref::MPD::PingInterval*1000, ping);
}

function MPDTCPObject::status(%this) {
	%this.lastCmd = "status";
	%this.line(%this.lastCmd);
}

function MPDTCPObject::stats(%this) {
	%this.lastCmd = "stats";
	%this.line(%this.lastCmd);
}

function MPDTCPObject::currentsong(%this) {
	%this.lastCmd = "currentsong";
	%this.line(%this.lastCmd);
}

function MPDTCPObject::replay_gain_status(%this) {
	%this.lastCmd = "replay_gain_status";
	%this.line(%this.lastCmd);
}

function MPDTCPObject::consume(%this, %state) {
	// since protocol v0.15
	%cmd = "consume";

	if(%state < 0 || %state > 1 || %state $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, 0 || 1)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %state);
}

function MPDTCPObject::crossfade(%this, %seconds) {
	%cmd = "crossfade";

	if(%seconds < 0 || %seconds $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, %seconds >= 0)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %seconds);
}

function MPDTCPObject::mixrampdb(%this, %decibels) {
	%cmd = "mixrampdb";

	if(%decibels > 0 || %decibels $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, %decibels <= 0)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %decibels);
}

function MPDTCPObject::mixrampdelay(%this, %seconds) {
	%cmd = "mixrampdelay";

	if(%seconds >= 0 || %seconds $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, %seconds >= 0 || nan)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %seconds);
}

function MPDTCPObject::random(%this, %state) {
	%cmd = "random";

	if(%state < 0 || %state > 1 || %state $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, 0 || 1)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %state);
}

function MPDTCPObject::repeat(%this, %state) {
	%cmd = "repeat";

	if(%state < 0 || %state > 1 || %state $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, 0 || 1)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %state);
}

function MPDTCPObject::setvol(%this, %percent) {
	%cmd = "setvol";

	if(%percent < 0 || %percent > 100 || %percent $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, 0-100)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %percent);
}

function MPDTCPObject::single(%this, %state) {
	%cmd = "single";

	if(%state < 0 || %state > 1 || %state $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, 0 || 1)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %state);
}

function MPDTCPObject::replay_gain_mode(%this, %mode) {
	%cmd = "replay_gain_mode";

	%valid = "off\ttrack\talbum\tauto\t";
	%mode = strLwr(%mode);

	if(stripos(%valid, %mode @ "\t") == -1) {
		warn("MPDTCPObject::" @ %cmd @ "(%this, off|track|album|auto)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %mode);
}

function MPDTCPObject::volume(%this, %v) {
	// deprecated, acting as a shortcut for setvol
	%this.setvol(%v);
}

function MPDTCPObject::pause(%this, %state) {
	%cmd = "pause";

	if(%state < 0 || %state > 1 || %state $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, 0 || 1)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %state);
}

function MPDTCPObject::next(%this) {
	%this.lastCmd = "next";
	%this.line(%this.lastCmd);
}

function MPDTCPObject::play(%this, %songpos) {
	%cmd = "play";

	if(%songpos < 0 || %songpos $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, %songpos)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %songpos);
}

function MPDTCPObject::playid(%this, %songid) {
	%cmd = "playid";

	if(%songid < 0 || %songid $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, %songid)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %songid);
}

function MPDTCPObject::previous(%this) {
	%this.lastCmd = "previous";
	%this.line(%this.lastCmd);
}

function MPDTCPObject::seek(%this, %songpos, %time) {
	%cmd = "seek";

	if(%songpos < 0 || %songpos $= "" || %time < 0 || %time $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, %songpos, %time)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %songpos SPC %time);
}

function MPDTCPObject::seekid(%this, %songid, %time) {
	%cmd = "seekid";

	if(%songid < 0 || %songid $= "" || %time < 0 || %time $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, %songid, %time)");
		return;
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %songid SPC %time);
}

function MPDTCPObject::seekcur(%this, %time) {
	%cmd = "seekcur";

	if(%time $= "") {
		warn("MPDTCPObject::" @ %cmd @ "(%this, seconds || +seconds|-seconds)");
		return;
	}

	if(stripos(%time, "+") == -1 && stripos(%time, "-") == -1 && %time < 0) {
		warn("MPDTCPObject::" @ %cmd @ "(%this, seconds || +seconds|-seconds)");
		return;		
	}

	%this.lastCmd = %cmd;
	%this.line(%cmd SPC %time);
}

function MPDTCPObject::stop(%this) {
	%this.lastCmd = "stop";
	%this.line(%this.lastCmd);
}