export function confirmSupervisor(feed, sender, messageTime, now=Date.now()) {
 const callsign=String(sender??'').trim().toUpperCase();
 const timestamp=Date.parse(feed?.general?.update_timestamp);
 if(!/^[A-Z0-9_-]{1,32}$/.test(callsign)||!Number.isFinite(messageTime)||now-messageTime<0||now-messageTime>30000)throw Error('Mensagem inválida ou antiga');
 if(!Number.isFinite(timestamp)||now-timestamp>60000||timestamp-now>5000||!Array.isArray(feed.controllers))throw Error('Feed indisponível ou desatualizado');
 const matches=feed.controllers.filter(c=>String(c.callsign??'').trim().toUpperCase()===callsign);
 const c=matches.length===1?matches[0]:null;
 const logon=Date.parse(c?.logon_time);
 const confirmed=!!c&&c.rating===11&&Number.isInteger(c.cid)&&c.cid>0&&Number.isFinite(logon)&&logon<=messageTime;
 return {ok:true,confirmed,callsign,rating:confirmed?11:0,cid:confirmed?String(c.cid):null,timestamp};
}
