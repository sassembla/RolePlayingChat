IDENTIFIER_CENTRAL = "central"
IDENTIFIER_CLIENT = "client"


STATE_CONNECT = 1
STATE_MESSAGE = 2
STATE_DISCONNECT_1 = 3
STATE_DISCONNECT_2 = 4


-- entrypoint for WebSocket client connection.


-- setup redis pub-sub
local redis = require "redis.redis"

local uuid = require "uuid.uuid"
local time = ngx.now() * 1000
uuid:setRandomSeed(time)

local serverId = uuid:getUUID()


subRedisCon = redis:new()
local ok, err = subRedisCon:connect("127.0.0.1", 6379)
if not ok then
	ngx.log(ngx.ERR, "connection:", serverId, " failed to generate subscriver")
	return
end
subRedisCon:set_timeout(1000 * 60 * 60)
local ok, err = subRedisCon:subscribe(IDENTIFIER_CLIENT)
if not ok then
	ngx.log(ngx.ERR, "connection:", serverId, " failed to start subscriver")
	return
end


pubRedisCon = redis:new()
local ok, err = pubRedisCon:connect("127.0.0.1", 6379)
if not ok then
	ngx.log(ngx.ERR, "connection:", serverId, " failed to generate publisher")
	return
end


-- setup websocket client
local wsServer = require "ws.websocketServer"

ws, wErr = wsServer:new{
	timeout = 10000000,
	max_payload_len = 65535
}

if not ws then
	ngx.log(ngx.ERR, "connection:", serverId, " failed to new websocket: ", wErr)
	return
end


function split(inputstr, sep)
	if sep == nil then
		sep = "%s"
	end
	local t = {}
	local i = 1
	for str in string.gmatch(inputstr, "([^"..sep.."]+)") do
		t[i] = str
		i = i + 1
	end
	return t
end

function connectWebSocket()
	-- start subscribe
	ngx.thread.spawn(subscribe)

	-- send connected
	local jsonData = "st"..STATE_CONNECT.."con"..serverId
	-- json:encode({connectionId = serverId, state = STATE_CONNECT})
	pubRedisCon:publish(IDENTIFIER_CENTRAL, jsonData)

	-- start websocket serving
	while true do
		local recv_data, typ, err = ws:recv_frame()

		if ws.fatal then
			local jsonData = "st"..STATE_DISCONNECT_1.."con"..serverId
			pubRedisCon:publish(IDENTIFIER_CENTRAL, jsonData)
			ngx.log(ngx.ERR, "connection:", serverId, " failed to send ping: ", err)
			break
		end
		if not recv_data then
			local bytes, err = ws:send_ping()
			if not bytes then
				ngx.log(ngx.ERR, "connection:", serverId, " failed to send ping: ", err)
				break
			end
		end

		if typ == "close" then
			local jsonData = "st"..STATE_DISCONNECT_2.."con"..serverId
			pubRedisCon:publish(IDENTIFIER_CENTRAL, jsonData)

			-- start close.
			break
		elseif typ == "ping" then
			local bytes, err = ws:send_pong()
			if not bytes then
				ngx.log(ngx.ERR, "connection:", serverId, " failed to send pong: ", err)
				break
			end
		elseif typ == "pong" then
			ngx.log(ngx.INFO, "client ponged")

		elseif typ == "text" then
			-- post message to central.
			local jsonData = "st"..STATE_MESSAGE.."con"..serverId..recv_data
			pubRedisCon:publish(IDENTIFIER_CENTRAL, jsonData)
		end
	end

	ws:send_close()
	ngx.log(ngx.ERR, "connection:", serverId, " connection closed")
end

-- subscribe loop
-- waiting data from central.
function subscribe ()
	while true do
		::continue::
		local res, err = subRedisCon:read_reply()
		if not res then
			ngx.log(ngx.ERR, "connection:", serverId, " redis subscribe read error:", err)
			break
		else
			if not ws:is_connecting() then
				subRedisCon:unsubscribe(IDENTIFIER_CLIENT)
				ngx.log(ngx.ERR, "connection:", serverId, " redis unsubscribed by websocket closed.")
				break
			end

			-- for i,v in ipairs(res) do
			-- 	ngx.log(ngx.ERR, "client i:", i, " v:", v)
			-- end

			-- send message with WebSocket for all subscribers.
			local dataSource = res[3]

			-- format is "cons" + countOfTargetConnectionIds + connectionId x n + data
			-- cons2:39C64DB7-7E6F-4264-B798-A5FA4A6483FA111111111111111111111111111111111111{"message":"you are joinning!"}

			local connectionsDesc = split(dataSource, ":")[1]
			local connectionCount = string.sub(connectionsDesc, 5) -- "cons" + 1

			-- if connection count is 0, this message is for all.
			if connectionCount == "0" then
				-- start point is definitely 7, "cons" + "0" + ":" + 1
				local data = string.sub(dataSource, 7)
				local bytes, err = ws:send_text(data)
				if not bytes then
					ngx.log(ngx.ERR, "connection:", serverId, " failed to send text 1:", err)
					break
				end
				goto continue
			end

			local sizeOfCount = string.len(tostring(connectionCount))
			-- first 5 is "cons" + 1
			local indexOfConnectionIds = 5 + sizeOfCount + 1
			local connectionDescLength = (connectionCount * 36) + (connectionCount - 1)
			local targetIdsDesc = string.sub(dataSource, indexOfConnectionIds, indexOfConnectionIds + connectionDescLength - 1)
			
			local targetIds = split(targetIdsDesc, ",")
			
			if contains(targetIds, serverId) then
				local indexOfData = indexOfConnectionIds + connectionDescLength
				local data = string.sub(dataSource, indexOfData)
				
				local bytes, err = ws:send_text(data)
				if not bytes then
					ngx.log(ngx.ERR, "connection:", serverId, " failed to send text 2:", err)
					break
				end
			end
		end
	end
end



function contains(tbl, item)
    for key, value in pairs(tbl) do
        if value == item then return key end
    end
    return false
end

connectWebSocket()
