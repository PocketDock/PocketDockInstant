$(document).ready(async () => {
    let inactiveTimerId = null;
    let getDataTimerId = null;
    let currentTab = "console-tab";
    let inactivityEndDate = null;
    let endDate = null;
    let currentTime = null;
    let initialized = false;
    let serverStopped = false;
    
    const titleDiv = document.querySelector('#term-title');

    /**
     * Time till date returned in hours, minutes, and seconds
     * @typedef {object} TimeTillDate
     * @property {number} hours Hours till date
     * @property {number} minutes Minutes (in the hour) till date
     * @property {number} seconds Seconds (in the minute) till date
     */

    async function startInactive() {
        async function GetData() {
            try {
                const res = await fetch("GetServerInfo");
                if (res.status === 410) {
                    serverStopped = true;
                    clearInterval(getDataTimerId);
                    clearInterval(inactiveTimerId);
                    $("#counter").text("00h:00m:00s");
                    $("#inactivemain").hide();
                    $('#files-tab').hide();
                    titleDiv.innerText = "Server stopped";
                    return;
                }
                serverData = await (res).json();
                inactivityEndDate = parseInt(serverData["inactivityEndDate"]);
                endDate = parseInt(serverData["endDate"]);
                currentTime = parseInt(serverData["currentTime"]);
            } catch (e) {
                console.error(e);
            }
        }
        await GetData();
        getDataTimerId = setInterval(GetData, 5000);
        inactiveTimerId = setInterval(async function() {
            currentTime = currentTime + 1000;
            const targetTime = timeTillDate(endDate);
            $("#counter").text(targetTime.hours + "h:" + targetTime.minutes + "m:" + targetTime.seconds + "s");
            if (!isNaN(inactivityEndDate)) {
                if (!initialized) {
                    initialized = true;
                }
                $("#inactivemain").show();
                const inactiveTargetTime = timeTillDate(inactivityEndDate);
                $("#inactivetime").text(inactiveTargetTime.minutes + "m:" + inactiveTargetTime.seconds + "s");
            } else {
                $("#inactivemain").hide();
            }
        }, 1000);
    }
    await startInactive();

    /**
     *
     * @param {number} targetDate Epoch time in seconds since the target date
     * @returns {TimeTillDate}
     */
    function timeTillDate(targetDate) {
        const timeLeft = (targetDate / 1000) - (currentTime / 1000);
        return {
            hours: Math.floor(timeLeft / 3600),
            minutes: Math.floor((timeLeft % 3600) / 60),
            seconds: Math.floor((timeLeft % 3600) % 60)
        }
    }

    function sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    while (true) {
        try
        {
            const res = await fetch($('#filesIframe')[0].src);
            if (res.ok) {
                //Reload frame
                $('#filesIframe')[0].src = $('#filesIframe')[0].src;
                $('#filesIframe').show();
                $('#filesLoading').hide();
                break;
            }
        } catch {}
        await sleep(3000);
    }

    while (true) {
        try
        {
            const res = await fetch($('#consoleIframe')[0].src);
            if (res.ok) {
                //Reload frame
                $('#consoleIframe')[0].src = $('#consoleIframe')[0].src;
                $('#console').show();
                $('#consoleLoading').hide();
                $('#consoleIframe')[0].addEventListener('load', x => {
                    new MutationObserver(function(mutations) {
                        titleDiv.innerText = mutations[0].target.innerText;
                    }).observe(
                        document.querySelector('#consoleIframe').contentWindow.document.querySelector('title'),
                        { subtree: true, characterData: true, childList: true }
                    );
                });
                break;
            }
        } catch {}
        await sleep(3000);
    }
});